using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    /// <summary>
    /// 导航的生命周期，需要ViewModel或Page实现该接口以接收导航事件的回调
    /// </summary>
    public interface IWindowAware
    {
        /// <summary>
        /// 新窗口即将打开时的回调方法
        /// </summary>
        /// <param name="parameters">新窗口携带的参数</param>
        void OnWindowNavigating(DialogParameters? parameters);

        /// <summary>
        /// 新窗口打开完成后的回调方法
        /// </summary>
        /// <param name="parameters">新窗口携带的参数</param>
        void OnWindowNavigated(DialogParameters? parameters);

        /// <summary>
        /// 当前窗口关闭前的回调方法
        /// </summary>
        /// <param name="parameters">当前窗口携带的参数</param>
        void OnWindowClosing(DialogParameters? parameters);

        /// <summary>
        /// 当前窗口关闭时的回调方法
        /// </summary>
        /// <param name="parameters">当前窗口携带的参数</param>
        void OnWindowClosed(DialogParameters? parameters);
    }
}
