using SimpleNavigation.Interface.Awares;
using System.Windows;

namespace SimpleNavigation.Common
{
    /// <summary>
    /// 对于Page或Content类型的导航回调
    /// </summary>
    internal static class NavigationAwareNotifier
    {
        public static void Notify(FrameworkElement target, DialogParameters? parameters)
        {
            if (target is IViewInitializeAware view && !view.IsViewInitialized)
            {
                view.Initialize();
                view.IsViewInitialized = true;
            }

            var dataContext = target.DataContext;

            if (dataContext is IViewInitializeAware contextViewAware
                && !ReferenceEquals(contextViewAware, target)
                && !contextViewAware.IsViewInitialized)
            {
                contextViewAware.Initialize();
                contextViewAware.IsViewInitialized = true;
            }

            if (target is INavigationAware targetAware)
                targetAware.OnNavigated(parameters);

            if (dataContext is INavigationAware contextAware && !ReferenceEquals(dataContext, target))
                contextAware.OnNavigated(parameters);
        }
    }
}


