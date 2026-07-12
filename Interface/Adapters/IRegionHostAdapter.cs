using SimpleNavigation.Common.Adapters;
using System.Windows;

namespace SimpleNavigation.Interface.Adapters
{
    /// <summary>
    /// Region 宿主适配器
    /// 若要实现其他类型控件作为宿主的能力，必须实现此接口
    /// </summary>
    internal interface IRegionHostAdapter
    {
        /// <summary>
        /// 宿主类型
        /// </summary>
        RegionHostKind Kind { get; }

        /// <summary>
        /// 标志当前 Adapter 可以处理的元素
        /// </summary>
        /// <param name="region">当前 Adapter 所拥有（对应）的 Region 实例</param>
        /// <returns></returns>
        bool CanHandle(FrameworkElement region);
    }
}
