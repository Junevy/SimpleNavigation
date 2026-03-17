using System.Windows.Controls;

namespace SimpleNavigation
{
    /// <summary>
    /// 路由实例对象
    /// </summary>
    /// <param name="pageType">当前路由实例对应的Page类型，在判断是否允许重复导航时使用</param>
    /// <param name="factory">路由实例的配置，详情见：<see cref="NavigationOptions"/></param>
    /// <param name="options">路由实例的构造方式，推荐使用<see cref="DependencyInjection"/>，以支持更复杂的页面构造需求。</param>
    internal class NavigationRoute(Type pageType, Func<Page> factory, NavigationOptions options)
    {
        /// <summary>
        /// 路由实例的构造方式，推荐使用<see cref="DependencyInjection"/>，以支持更复杂的页面构造需求。
        /// </summary>
        private readonly Func<Page> factory = factory;

        /// <summary>
        /// 路由实例的配置，详情见：<see cref="NavigationOptions"/>
        /// </summary>
        public NavigationOptions Options { get; } = options;

        /// <summary>
        /// 当前路由实例对应的Page类型，在判断是否允许重复导航时使用
        /// </summary>
        public Type PageType { get; } = pageType;

        /// <summary>
        /// 单例模式下的缓存页面实例，首次导航时创建并缓存，后续导航直接返回该实例；瞬态模式下不使用该字段。
        /// </summary>
        private Page? cachePage;


        /// <summary>
        /// 构造路由示例
        /// </summary>
        /// <returns>路由实例</returns>
        public Page GetPage()
        {
            if (Options.AllowMulti == NavigationOptions.PageMode.Singleton)
                return cachePage ??= factory();

            return factory();
        }
    }
}
