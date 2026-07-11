using System.Windows;
using SimpleNavigation.Interface;

namespace SimpleNavigation.Common;

public sealed class RegionManager : IRegionManager
{
    private static readonly IRegionHostAdapter[] HostAdapters =
    {
        new FrameRegionAdapter(),
        new ContentControlRegionAdapter(),
    };

    private readonly Dictionary<string, WeakReference<FrameworkElement>> regions =
        new(StringComparer.Ordinal);

    public void RegisterRegion(string regionName, FrameworkElement region)
    {
        ValidateRegionName(regionName);

        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        ResolveHostAdapter(region);

        if (regions.TryGetValue(regionName, out var existingReference) &&
            existingReference.TryGetTarget(out var existingRegion))
        {
            if (ReferenceEquals(existingRegion, region))
            {
                return;
            }

            throw new InvalidOperationException($"Region '{regionName}' is already registered.");
        }

        regions[regionName] = new WeakReference<FrameworkElement>(region);
    }

    public bool UnregisterRegion(string regionName, FrameworkElement region)
    {
        ValidateRegionName(regionName);

        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (!regions.TryGetValue(regionName, out var existingReference))
        {
            return false;
        }

        if (!existingReference.TryGetTarget(out var existingRegion))
        {
            regions.Remove(regionName);
            return false;
        }

        if (!ReferenceEquals(existingRegion, region))
        {
            return false;
        }

        return regions.Remove(regionName);
    }

    public FrameworkElement? GetRegion(string regionName)
    {
        ValidateRegionName(regionName);

        if (!regions.TryGetValue(regionName, out var regionReference))
        {
            return null;
        }

        if (regionReference.TryGetTarget(out var region))
        {
            return region;
        }

        regions.Remove(regionName);
        return null;
    }

    public TRegion? GetRegion<TRegion>(string regionName) where TRegion : FrameworkElement
    {
        return GetRegion(regionName) as TRegion;
    }

    private static void ValidateRegionName(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName))
        {
            throw new ArgumentException("Region name cannot be null, empty, or whitespace.", nameof(regionName));
        }
    }

    private static IRegionHostAdapter ResolveHostAdapter(FrameworkElement region)
    {
        foreach (var adapter in HostAdapters)
        {
            if (adapter.CanHandle(region))
            {
                return adapter;
            }
        }

        var regionType = region.GetType();
        throw new ArgumentException(
            $"Region host type '{regionType.FullName}' is not supported.",
            nameof(region));
    }
}
