using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    /// <summary>
    /// 导航的生命周期，需要ViewModel或Page实现该接口以接收导航事件的回调
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// 导航即将发生时的回调方法
        /// </summary>
        /// <param name="parameters">导航携带的参数</param>
        void OnNavigating(DialogParameters? parameters);

        /// <summary>
        /// 导航完成后的回调方法
        /// </summary>
        /// <param name="parameters"></param>
        void OnNavigated(DialogParameters? parameters);
    }
}
