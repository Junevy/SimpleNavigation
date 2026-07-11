using System.Windows;
using SimpleNavigation.Interface;
using SimpleNavigation.Services;

namespace SimpleNavigation.Common;

public sealed class RegionManager : IRegionManager, IDisposable
{
    private readonly Dictionary<string, RegionEntry> regions =
        new(StringComparer.Ordinal);
    private readonly List<RegionDeclarationChange> pendingDeclarationChanges = new();
    private readonly object syncRoot = new();
    private bool isImportingDeclarations = true;
    private bool isDisposed;

    public RegionManager()
    {
        Region.Subscribe(OnDeclarationChanged);

        try
        {
            var snapshot = Region.GetActiveSnapshot();

            lock (syncRoot)
            {
                foreach (var change in snapshot)
                {
                    ApplyDeclarationChangeUnderLock(change);
                }

                foreach (var change in pendingDeclarationChanges)
                {
                    ApplyDeclarationChangeUnderLock(change);
                }

                pendingDeclarationChanges.Clear();
                isImportingDeclarations = false;
            }
        }
        catch
        {
            lock (syncRoot)
            {
                isDisposed = true;
                isImportingDeclarations = false;
                pendingDeclarationChanges.Clear();
                regions.Clear();
            }

            Region.Unsubscribe(OnDeclarationChanged);
            throw;
        }
    }

    public void RegisterRegion(string regionName, FrameworkElement region)
    {
        ValidateRegionName(regionName);

        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        RegionHostAdapterResolver.GetRequired(region);

        lock (syncRoot)
        {
            ThrowIfDisposedUnderLock();

            if (TryGetLiveEntryUnderLock(regionName, out var existingEntry, out var existingRegion))
            {
                if (ReferenceEquals(existingRegion, region))
                {
                    existingEntry.HasProgrammaticOwnership = true;
                    return;
                }

                throw new InvalidOperationException($"Region '{regionName}' is already registered.");
            }

            regions[regionName] = new RegionEntry(region)
            {
                HasProgrammaticOwnership = true,
            };
        }
    }

    public bool UnregisterRegion(string regionName, FrameworkElement region)
    {
        ValidateRegionName(regionName);

        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        lock (syncRoot)
        {
            ThrowIfDisposedUnderLock();

            if (!TryGetLiveEntryUnderLock(regionName, out var existingEntry, out var existingRegion))
            {
                return false;
            }

            if (!ReferenceEquals(existingRegion, region) ||
                !existingEntry.HasProgrammaticOwnership)
            {
                return false;
            }

            existingEntry.HasProgrammaticOwnership = false;
            if (existingEntry.AttachedActivationTokens.Count == 0)
            {
                regions.Remove(regionName);
            }

            return true;
        }
    }

    public FrameworkElement? GetRegion(string regionName)
    {
        ValidateRegionName(regionName);

        lock (syncRoot)
        {
            ThrowIfDisposedUnderLock();

            if (TryGetLiveEntryUnderLock(regionName, out _, out var region))
            {
                return region;
            }

            return null;
        }
    }

    public TRegion? GetRegion<TRegion>(string regionName) where TRegion : FrameworkElement
    {
        return GetRegion(regionName) as TRegion;
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            isImportingDeclarations = false;
            pendingDeclarationChanges.Clear();
            regions.Clear();
        }

        Region.Unsubscribe(OnDeclarationChanged);
    }

    private void OnDeclarationChanged(RegionDeclarationChange change)
    {
        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            if (isImportingDeclarations)
            {
                pendingDeclarationChanges.Add(change);
                return;
            }

            ApplyDeclarationChangeUnderLock(change);
        }
    }

    private void ApplyDeclarationChangeUnderLock(RegionDeclarationChange change)
    {
        if (change.Kind == RegionDeclarationChangeKind.Add)
        {
            AddAttachedOwnershipUnderLock(change);
            return;
        }

        RemoveAttachedOwnershipUnderLock(change);
    }

    private void AddAttachedOwnershipUnderLock(RegionDeclarationChange change)
    {
        var host = change.Host;

        if (TryGetLiveEntryUnderLock(change.Name, out var existingEntry, out var existingHost))
        {
            if (!ReferenceEquals(existingHost, host))
            {
                throw new InvalidOperationException($"Region '{change.Name}' is already registered.");
            }

            existingEntry.AttachedActivationTokens.Add(change.ActivationToken);
            return;
        }

        var entry = new RegionEntry(host);
        entry.AttachedActivationTokens.Add(change.ActivationToken);
        regions[change.Name] = entry;
    }

    private void RemoveAttachedOwnershipUnderLock(RegionDeclarationChange change)
    {
        if (!TryGetLiveEntryUnderLock(change.Name, out var existingEntry, out var existingHost) ||
            !ReferenceEquals(existingHost, change.Host))
        {
            return;
        }

        existingEntry.AttachedActivationTokens.Remove(change.ActivationToken);
        if (!existingEntry.HasProgrammaticOwnership &&
            existingEntry.AttachedActivationTokens.Count == 0)
        {
            regions.Remove(change.Name);
        }
    }

    private bool TryGetLiveEntryUnderLock(
        string regionName,
        out RegionEntry entry,
        out FrameworkElement region)
    {
        if (!regions.TryGetValue(regionName, out entry!))
        {
            region = null!;
            return false;
        }

        if (entry.Host.TryGetTarget(out region!))
        {
            return true;
        }

        regions.Remove(regionName);
        entry = null!;
        region = null!;
        return false;
    }

    private void ThrowIfDisposedUnderLock()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(RegionManager));
        }
    }

    private static void ValidateRegionName(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName))
        {
            throw new ArgumentException("Region name cannot be null, empty, or whitespace.", nameof(regionName));
        }
    }

    private sealed class RegionEntry
    {
        public RegionEntry(FrameworkElement host)
        {
            Host = new WeakReference<FrameworkElement>(host);
        }

        public WeakReference<FrameworkElement> Host { get; }

        public bool HasProgrammaticOwnership { get; set; }

        public HashSet<long> AttachedActivationTokens { get; } = new();
    }
}
