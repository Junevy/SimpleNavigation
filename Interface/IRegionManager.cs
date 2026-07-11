using System.Windows;

namespace SimpleNavigation.Interface;

public interface IRegionManager
{
    void RegisterRegion(string regionName, FrameworkElement region);

    bool UnregisterRegion(string regionName, FrameworkElement region);

    FrameworkElement? GetRegion(string regionName);

    TRegion? GetRegion<TRegion>(string regionName) where TRegion : FrameworkElement;
}
