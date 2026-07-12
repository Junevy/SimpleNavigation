using System.Windows;
using SimpleNavigation.Common.Adapters;
using SimpleNavigation.Interface.Managers;
using SimpleNavigation.Services;

namespace SimpleNavigation.Common.Managers
{
    public sealed class RegionManager : IRegionManager, IDisposable
    {
        private readonly Dictionary<string, RegionEntry> regions = new(StringComparer.Ordinal);
        private readonly Action<RegionDeclarationChange> declarationSubscriber;
        private readonly List<RegionDeclarationChange> pendingDeclarationChanges = new();
        private readonly object syncRoot = new();
        private bool isImportingDeclarations = true;
        private bool isDisposed;

        public RegionManager()
        {
            declarationSubscriber = OnDeclarationChanged;
            Region.Subscribe(declarationSubscriber);

            try
            {
                var snapshot = Region.GetActiveSnapshot();

                lock (syncRoot)
                {
                    foreach (var change in snapshot)
                        ApplyDeclarationChangeUnderLock(change);

                    foreach (var change in pendingDeclarationChanges)
                        ApplyDeclarationChangeUnderLock(change);

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

                Region.Unsubscribe(declarationSubscriber);
                throw;
            }
        }

        public void RegisterRegion(string regionName, FrameworkElement region)
        {
            ValidateRegionName(regionName);
            if (region == null) throw new ArgumentNullException(nameof(region));

            RegionHostAdapterResolver.GetRequired(region);  // 无匹配的Adapter则抛出异常

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
            => GetRegion(regionName) as TRegion;

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

            Region.Unsubscribe(declarationSubscriber);
        }

        /// <summary>
        /// 当宿主容器属性发生变化事件回调
        /// </summary>
        /// <param name="change">变化事件携带信息</param>
        private void OnDeclarationChanged(RegionDeclarationChange change)
        {
            lock (syncRoot)
            {
                if (isDisposed)
                    return;

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

        /// <summary>
        /// 获取活跃的
        /// </summary>
        /// <param name="regionName"></param>
        /// <param name="entry"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        private bool TryGetLiveEntryUnderLock(string regionName, out RegionEntry entry, out FrameworkElement region)
        {
            if (!regions.TryGetValue(regionName, out entry!))
            {
                region = null!;
                return false;
            }

            if (entry.Host.TryGetTarget(out region!))
                return true;

            regions.Remove(regionName);
            entry = null!;
            region = null!;
            return false;
        }

        private void ThrowIfDisposedUnderLock()
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(RegionManager));
        }

        private static void ValidateRegionName(string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                throw new ArgumentException("Region name cannot be null, empty, or whitespace.", nameof(regionName));
        }
    }

    internal sealed class RegionEntry
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


