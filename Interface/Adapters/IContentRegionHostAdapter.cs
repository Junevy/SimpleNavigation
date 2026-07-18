using System.Windows;

namespace SimpleNavigation.Interface.Adapters
{
    /// <summary>
    /// 容纳 <see cref="FrameworkElement"/> 对象的宿主适配器
    /// </summary>
    internal interface IContentRegionHostAdapter : IRegionHostAdapter
    {
        /// <summary>
        /// 导航核心实现方法：将需要导航的内容加入到宿主的视觉树
        /// </summary>
        /// <param name="host">宿主</param>
        /// <param name="content">需要导航的内容</param>
        void Present(FrameworkElement host, FrameworkElement content);
    }
}
