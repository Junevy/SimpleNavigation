using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common;

internal sealed class ContentControlRegionAdapter : IRegionHostAdapter
{
    public RegionHostKind Kind => RegionHostKind.Content;

    public bool CanHandle(FrameworkElement region)
    {
        return region is ContentControl && region is not Frame;
    }
}
