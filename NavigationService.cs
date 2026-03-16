using System.Collections.Concurrent;
using System.Windows.Controls;

namespace SimpleNavigation
{
    public class NavigationService() : INavigationService
    {
        //private IServiceProvider serviceProvider = serviceProvider;
        private readonly ConcurrentDictionary<string, NavigationRoute> Routes = new();
        private readonly ConcurrentDictionary<string, Frame> Regions = new();

        /// <summary>
        /// 注册路由
        /// </summary>
        /// <typeparam name="TPage">需要导航过的Page</typeparam>
        /// <param name="route">对应Page的Key</param>
        public void RegisterRoute<TPage>(NavigationOptions? options = null) where TPage : Page, new()
        {
            RegisterRoute(nameof(TPage), () => new TPage(), options);
        }

        public void RegisterRoute<TPage>(string route, NavigationOptions? options = null) where TPage : Page, new()
        {
            RegisterRoute<TPage>(route, () => new TPage(), options);
        }

        public void RegisterRoute<TPage>(string route, Func<Page> factory, NavigationOptions? options = null)
        {
            RegisterRoute(route, factory, options, typeof(TPage));
        }

        public void RegisterRoute<TPage>(Func<Page> factory, NavigationOptions? options = null)
        {
            RegisterRoute(nameof(TPage), factory, options, typeof(TPage));
        }

        public void RegisterRoute(string route, Func<Page> factory, NavigationOptions? options = null, Type? pageType = null)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Route cannot be null or whitespace.", nameof(route));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory cannot be null.");

            options ??= new NavigationOptions();
            Routes[route] = new NavigationRoute(pageType ?? factory.GetType(), factory, options);
        }



        /// <summary>
        /// 注册导航区域
        /// </summary>
        /// <typeparam name="TType">限制Region的类型为Frame</typeparam>
        /// <param name="region">Region的Key</param>
        /// <param name="targetRegion">Region对象</param>
        public void RegisterRegion(string regionName, Frame region)
        {
            if (!string.IsNullOrWhiteSpace(regionName) && region != null)
                Regions[regionName] = region;
        }

        /// <summary>
        /// 导航功能的实现
        /// </summary>
        /// <typeparam name="TPage">导航目标类型</typeparam>
        /// <param name="region">导航区域的Key</param>
        public void Navigate<TPage>(string region)
        {
            Navigate(region, nameof(TPage), new NavigationParameter());
        }

        /// <summary>
        /// 导航功能的实现
        /// </summary>
        /// <param name="route">对应Page的Key</param>
        /// <param name="region">导航区域的Key</param>
        public void Navigate(string region, string route, NavigationParameter parameters)
        {
            if (!Routes.TryGetValue(route, out var targetPage)) return;
            if (!Regions.TryGetValue(region, out var targetRegion)) return;

            if (targetPage.Options.AllowMulti == NavigationOptions.PageMode.Singleton)
            {
                if (targetRegion.Content?.GetType() == targetPage.PageType)
                    return;
            }

            var pageInstance = targetPage.GetPage();
            InvokeNavigating(targetRegion.Content, parameters);
            targetRegion.Navigate(pageInstance);
            InvokeNavigated(pageInstance, parameters);

            // 清除导航历史
            if (targetPage.Options.History == NavigationOptions.KeepHistory.Never)
            {
                while (targetRegion.CanGoBack)
                    targetRegion.RemoveBackEntry();
            }
        }

        private void InvokeNavigating(object? oldPage, NavigationParameter parameters)
        {
            if (oldPage is INavigationAware aware)
                aware.OnNavigating(parameters);

            if (oldPage is Page page && page.DataContext is INavigationAware vmAware)
                vmAware.OnNavigating(parameters);
        }

        private void InvokeNavigated(Page pageInstance, NavigationParameter parameters)
        {
            if (pageInstance is INavigationAware aware)
                aware.OnNavigated(parameters);

            if (pageInstance.DataContext is INavigationAware vmAware)
                vmAware.OnNavigated(parameters);
        }

        public void Goback(string region)
        {
            if (Regions.TryGetValue(region, out var frame))
            {
                if (frame.CanGoBack)
                    frame.GoBack();
            }
        }

        public bool UnRegisterRoute<TPage>() where TPage : Page
        {
            return Routes.TryRemove(nameof(TPage), out _);
        }

        public bool UnRegisterRoute(string route)
        {
            return Routes.TryRemove(route, out _);
        }

        public bool UnRegisterRegion(string region)
        {
            return Regions.TryRemove(region, out _);
        }
    }
}
