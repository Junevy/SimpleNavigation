using System.Windows.Controls;

namespace SimpleNavigation.Common
{
    /// <summary>
    /// 路由实例对象
    /// </summary>
    /// <param name="pageType">当前路由实例对应的Page类型，在判断是否允许重复导航时使用</param>
    /// <param name="factory">路由实例的配置，详情见：<see cref="DialogOptions"/></param>
    /// <param name="options">路由实例的构造方式，推荐使用<see cref="DependencyInjection"/>，以支持更复杂的页面构造需求。</param>
    internal class NavigationRoute
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public Guid Id { get; }

        public NavigationRoute(Type pageType, Func<Page> factory, DialogOptions options)
        {
            Id = new Guid();
            this.factory = factory;
            Options = options;
            PageType = pageType;
        }

        /// <summary>
        /// 路由实例的构造方式，推荐使用<see cref="DependencyInjection"/>，以支持更复杂的页面构造需求。
        /// </summary>
        private readonly Func<Page> factory;

        /// <summary>
        /// 路由实例的配置，详情见：<see cref="DialogOptions"/>
        /// </summary>
        public DialogOptions Options { get; }

        /// <summary>
        /// 当前路由实例对应的Page类型，在判断是否允许重复导航时使用
        /// </summary>
        public Type PageType { get; }

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
            if (Options.AllowMulti == DialogOptions.DialogMode.Singleton)
                return cachePage ??= factory();

            return factory();
        }
    }
}
