using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    public interface IPageAware
    {
        /// <summary>
        /// 当页面接收消息时的回调方法
        /// </summary>
        /// <param name="parameters">消息参数</param>
        event Action<DialogParameters?>? Receive;

        /// <summary>
        /// 当页面导航完成时的回调方法
        /// </summary>
        /// <param name="parameters">导航参数</param>
        void OnNavigated(DialogParameters? parameters);
    }
}
