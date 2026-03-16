using System.Windows.Controls;

namespace SimpleNavigation
{
    public interface INavigationService
    {
        /// <summary>
        /// 返回当前区域的上一个页面，前提是该区域的导航历史被保留（<see cref="NavigationOptions.History"/>设置为<see cref="NavigationOptions.KeepHistory.Always"/>）。
        /// </summary>
        /// <param name="region"></param>
        void Goback(string region);

        /// <summary>
        /// 导航到新的页面
        /// </summary>
        /// <param name="region"></param>
        /// <param name="route"></param>
        /// <param name="parameters"></param>
        void Navigate(string region, string route, NavigationParameter parameters);
        void Navigate<TPage>(string region);

        void RegisterRegion(string regionName, Frame region);

        void RegisterRoute(string route, Func<Page> factory, NavigationOptions? options = null, Type? pageType = null);
        void RegisterRoute<TPage>(NavigationOptions? options = null) where TPage : Page, new();
        void RegisterRoute<TPage>(Func<Page> factory, NavigationOptions? options = null);
        void RegisterRoute<TPage>(string route, NavigationOptions? options = null) where TPage : Page, new();
        void RegisterRoute<TPage>(string route, Func<Page> factory, NavigationOptions? options = null);

        bool UnRegisterRegion(string region);

        bool UnRegisterRoute(string route);
        bool UnRegisterRoute<TPage>() where TPage : Page;
    }
}