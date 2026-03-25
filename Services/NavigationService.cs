using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Collections.Concurrent;
using System.Windows.Controls;

namespace SimpleNavigation.Services
{
    public class NavigationService : INavigationService
    {
        private readonly ISerilog logger;

        // 维护的路由表
        private readonly ConcurrentDictionary<string, NavigationRoute> Routes = new();

        //维护的导航区域表
        private readonly ConcurrentDictionary<string, Frame> Regions = new();

        public NavigationService(ISerilog logger)
        {
            // 订阅注册Region事件，允许在XAML中使用RegionService注册导航区域
            RegionService.RegionRegisted += (regionName, frame) => RegisterRegion(regionName, frame);

            this.logger = logger; 
        }


        public void RegisterRoute<TPage>(NavigationOptions? options = null) where TPage : Page, new()
        {
            RegisterRoute(nameof(TPage), () => new TPage(), options);
        }


        public void RegisterRoute<TPage>(string route, NavigationOptions? options = null) where TPage : Page, new()
        {
            RegisterRoute<TPage>(route, () => new TPage(), options);
        }


        public void RegisterRoute<TPage>(string route, Func<Page> factory, NavigationOptions? options = null) where TPage : Page
        {
            RegisterRoute(route, factory, options, typeof(TPage));
        }


        public void RegisterRoute<TPage>(Func<Page> factory, NavigationOptions? options = null)
        {
            RegisterRoute(typeof(TPage).ToString(), factory, options, typeof(TPage));
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


        public void RegisterRegion(string regionName, Frame region)
        {
            if (!string.IsNullOrWhiteSpace(regionName) && region != null)
                Regions[regionName] = region;
        }


        public void Navigate<TPage>(string region, NavigationParameter? parameter = null)
        {
            Navigate(region, typeof(TPage).ToString(), parameter);
        }

        public void Navigate(string region, string route, NavigationParameter? parameters = null)
        {
            if (!Routes.TryGetValue(route, out var targetPage)) return;
            if (!Regions.TryGetValue(region, out var targetRegion)) return;

            // 通过配置约束：不允许多次导航到同一类型的Page，避免重复创建页面实例和触发导航事件
            if (targetPage.Options.AllowMulti == NavigationOptions.PageMode.Singleton)
            {
                if (targetRegion.Content?.GetType() == targetPage.PageType)
                    return;
            }

            var pageInstance = targetPage.GetPage();

            // 触发相应的导航事件
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

        /// <summary>
        /// 导航事件触发器，在导航前触发，允许页面或其ViewModel执行特定逻辑，如取消导航、准备数据等。触发时机：在调用<see cref="Frame.Navigate(object)"/>方法之前。
        /// </summary>
        /// <param name="oldPage">导航区域当前的Page</param>
        /// <param name="parameters">导航时所携带的参数，详情见：<see cref="NavigationParameter"/></param>
        private void InvokeNavigating(object? oldPage, NavigationParameter parameters)
        {
            if (oldPage is INavigationAware aware)
                aware.OnNavigating(parameters);

            if (oldPage is Page page && page.DataContext is INavigationAware vmAware)
                vmAware.OnNavigating(parameters);
        }

        /// <summary>
        /// 导航事件触发器，在导航后触发，允许页面或其ViewModel执行特定逻辑，如取消导航、准备数据等。触发时机：在调用<see cref="Frame.Navigate(object)"/>方法之后。
        /// </summary>
        /// <param name="pageInstance"></param>
        /// <param name="parameters"></param>
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
            return Routes.TryRemove(typeof(TPage).ToString(), out _);
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
