using SimpleNavigation.Interface.Awares;
using System.Windows;

namespace SimpleNavigation.Common;

internal static class NavigationAwareNotifier
{
    public static void Notify(FrameworkElement target, DialogParameters? parameters)
    {
        if (target is INavigationAware targetAware)
            targetAware.OnNavigated(parameters);

        var dataContext = target.DataContext;

        if (dataContext is INavigationAware contextAware && !ReferenceEquals(dataContext, target))
            contextAware.OnNavigated(parameters);
    }
}
