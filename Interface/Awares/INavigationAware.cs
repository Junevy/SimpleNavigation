using SimpleNavigation.Common;

namespace SimpleNavigation.Interface.Awares
{
    /// <summary>
    /// 导航完成后的回调接口，
    /// 若需导航后执行回调（传递参数），View 或 ViewModel必须实现此接口
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// 导航后的回调方法
        /// </summary>
        /// <param name="parameters">传递的参数</param>
        void OnNavigated(DialogParameters? parameters);
    }
}


