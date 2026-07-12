using System.Windows;

namespace SimpleNavigation.Interface.Managers
{
    /// <summary>
    /// Region管理对象，负责Region的注册、注销、获取
    /// </summary>
    public interface IRegionManager
    {
        /// <summary>
        /// 将指定控件注册为 Region
        /// </summary>
        /// <param name="regionName">Region名称</param>
        /// <param name="region">Region 对象（指定为 Region 的实例控件）</param>
        void RegisterRegion(string regionName, FrameworkElement region);

        /// <summary>
        /// 将指定控件从 Region 中注销
        /// </summary>
        /// <param name="regionName">Region 名称</param>
        /// <param name="region">Region 对象（指定为 Region 的实例控件）</param>
        /// <returns>是否成功注销</returns>
        bool UnregisterRegion(string regionName, FrameworkElement region);

        /// <summary>
        /// 获取指定 Region 实例
        /// </summary>
        /// <param name="regionName">Region 名称</param>
        /// <returns>获得的 Region 实例</returns>
        FrameworkElement? GetRegion(string regionName);

        /// <summary>
        /// 获取指定类型的 Region 实例
        /// </summary>
        /// <typeparam name="TRegion">指定的控件类型</typeparam>
        /// <param name="regionName">Region 名称</param>
        /// <returns>指定类型控件的实例</returns>
        TRegion? GetRegion<TRegion>(string regionName) where TRegion : FrameworkElement;
    }
}


