using System.Windows.Controls;

namespace SimpleNavigation.Interface.Adapters
{
    /// <summary>
    /// 容纳 <see cref="Page"/> 对象的宿主适配器
    /// </summary>
    internal interface IPageRegionHostAdapter : IRegionHostAdapter
    {
        bool Navigate(Frame frame, Page page);

        bool CanGoBack(Frame frame);

        void GoBack(Frame frame);
    }
}
