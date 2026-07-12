using SimpleNavigation.Common;

namespace SimpleNavigation.Interface.Awares
{
    /// <summary>
    /// Page导航回调接口，
    /// 若要实现Page导航后执行回调（传递参数），View或ViewModel必须实现此接口
    /// </summary>
    public interface IPageAware : INavigationAware
    {
        /// <summary>
        /// 当页面接收消息时的回调方法
        /// </summary>
        /// <param name="parameters">消息参数</param>
        event Action<DialogParameters?>? Receive;
    }
}
