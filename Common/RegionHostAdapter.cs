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
