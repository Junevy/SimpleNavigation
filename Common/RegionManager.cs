using System.Windows;
using SimpleNavigation.Interface;

namespace SimpleNavigation.Common;

public sealed class RegionManager : IRegionManager
{
    private readonly Dictionary<string, WeakReference<FrameworkElement>> regions =
        new(StringComparer.Ordinal);
    private readonly object syncRoot = new();

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
    }

    public FrameworkElement? GetRegion(string regionName)
    {
        ValidateRegionName(regionName);

        lock (syncRoot)
        {
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
}
