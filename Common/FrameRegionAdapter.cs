using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common;

internal sealed class FrameRegionAdapter : IRegionHostAdapter
{
    public RegionHostKind Kind => RegionHostKind.Page;

    public bool CanHandle(FrameworkElement region)
    {
        return region is Frame;
    }
}
