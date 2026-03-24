using SimpleNavigation.Common;
using System.Windows.Controls;

namespace SimpleNavigation.Interface
{
    public interface INavigationService
    {
        /// <summary>
        /// 返回上一页
        /// </summary>
        /// <param name="region"></param>
        void Goback(string region);

        /// <summary>
        /// 导航功能，根据提供的路由Key和区域Key导航到目标路由（Page），并且可以传递参数。导航过程中会触发页面的<see cref="INavigationAware"/>接口方法，允许在导航前后执行特定逻辑。
        /// </summary>
        /// <param name="region">导航的目标区域</param>
        /// <param name="route">导航的目标路由</param>
        /// <param name="parameters">导航时所携带的参数，详情见：<see cref="NavigationParameter"/></param>
        void Navigate(string region, string route, NavigationParameter? parameters = null);

        /// <summary>
        /// 导航功能，根据提供的路由Key和区域Key导航到目标路由（Page），并且可以传递参数。导航过程中会触发页面的<see cref="INavigationAware"/>接口方法，允许在导航前后执行特定逻辑。
        /// </summary>
        /// <typeparam name="TPage">导航的目标路由。通过TPage.FullName来获取指定的路由Key</typeparam>
        /// <param name="region">导航的目标区域</param>
        /// <param name="parameter">导航时所携带的参数，详情见：<see cref="NavigationParameter"/></param>
        void Navigate<TPage>(string region, NavigationParameter? parameters = null);

        /// <summary>
        /// 注册区域，区域是导航的目标容器，允许在不同的区域导航到不同的页面，甚至在同一区域导航到同一个页面但传递不同的参数。
        /// </summary>
        /// <param name="regionName">区域的Key</param>
        /// <param name="region">类型：<see cref="Frame"/>，用于不同Page的导航</param>
        void RegisterRegion(string regionName, Frame region);

        /// <summary>
        /// 注册路由，要求提供Page的构造方式，推荐使用<see cref="DependencyInjection"/>
        /// </summary>
        /// <param name="route">路由Key，<see cref="Routes"/></param>
        /// <param name="factory">路由对象的构造方式</param>
        /// <param name="options">详情查看<see cref="NavigationOptions"/></param>
        /// <param name="pageType">记录Page类型，用于判断是否能重复导航，<see cref="NavigationRoute.PageType"/></param>
        void RegisterRoute(string route, Func<Page> factory, NavigationOptions? options = null, Type? pageType = null);

        /// <summary>
        /// 注册路由，默认使用类型名称作为路由Key，并且要求Page具有无参构造函数。
        /// </summary>
        /// <typeparam name="TPage">记录路由的Page类型，同时作为路由Key，<see cref="NavigationRoute.PageType"/></typeparam>
        /// <param name="options">详情查看<see cref="NavigationOptions"/></param>
        void RegisterRoute<TPage>(NavigationOptions? options = null) where TPage : Page, new();

        /// <summary>
        /// 注册路由，要求提供Page的构造方式，推荐使用<see cref="DependencyInjection"/>
        /// </summary>
        /// <typeparam name="TPage">记录路由的Page类型，<see cref="NavigationRoute.PageType"/></typeparam>
        /// <param name="factory">路由对象的构造方式</param>
        /// <param name="options">详情查看<see cref="NavigationOptions"/></param>
        void RegisterRoute<TPage>(Func<Page> factory, NavigationOptions? options = null);

        /// <summary>
        /// 注册路由，要求Page具有无参构造函数。
        /// </summary>
        /// <typeparam name="TPage">记录路由的Page类型，<see cref="NavigationRoute.PageType"/></typeparam>
        /// <param name="route">路由Key，<see cref="Routes"/></param>
        /// <param name="options">详情查看<see cref="NavigationOptions"/></param>
        void RegisterRoute<TPage>(string route, NavigationOptions? options = null) where TPage : Page, new();

        /// <summary>
        /// 注册路由，要求提供Page的构造方式，推荐使用<see cref="DependencyInjection"/>
        /// </summary>
        /// <typeparam name="TPage">记录路由的Page类型，<see cref="NavigationRoute.PageType"/></typeparam>
        /// <param name="route">路由Key，<see cref="Routes"/></param>
        /// <param name="factory">路由对象的构造方式</param>
        /// <param name="options">详情查看<see cref="NavigationOptions"/></param>
        void RegisterRoute<TPage>(string route, Func<Page> factory, NavigationOptions? options = null) where TPage : Page;

        /// <summary>
        /// 取消注册区域，根据提供的区域Key从区域表中移除对应的区域记录，取消注册后将无法通过该区域进行导航。
        /// </summary>
        /// <param name="region">取消注册区域的Key</param>
        /// <returns>是否成功取消注册</returns>
        bool UnRegisterRegion(string region);

        /// <summary>
        /// 取消注册路由，根据提供的路由Key或Page类型从路由表中移除对应的路由记录，取消注册后将无法通过该路由进行导航。
        /// </summary>
        /// <param name="route">取消注册路由的Key</param>
        /// <returns>是否成功取消注册</returns>
        bool UnRegisterRoute(string route);

        /// <summary>
        /// 取消注册路由，根据提供的路由Key或Page类型从路由表中移除对应的路由记录，取消注册后将无法通过该路由进行导航。
        /// </summary>
        /// <typeparam name="TPage">取消注册路由的目标类型</typeparam>
        /// <returns>是否成功取消注册</returns>
        bool UnRegisterRoute<TPage>() where TPage : Page;

        Frame GetRegion(string regionName);
    }
}