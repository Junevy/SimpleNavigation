using System.Windows;

namespace SimpleNavigation.Common;

internal enum RegionHostKind
{
    Page,
    Content,
}

internal interface IRegionHostAdapter
{
    RegionHostKind Kind { get; }

    bool CanHandle(FrameworkElement region);
}

internal static class RegionHostAdapterResolver
{
    private static readonly IRegionHostAdapter[] HostAdapters =
    {
        new FrameRegionAdapter(),
        new ContentControlRegionAdapter(),
    };

    public static IRegionHostAdapter GetRequired(FrameworkElement region)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

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
