using SimpleNavigation.Common;

namespace SimpleNavigation.Interface.Awares
{
    /// <summary>
    /// Window导航回调接口，
    /// 若要实现Window导航后或关闭后执行回调（传递参数），View或ViewModel必须实现此接口
    /// </summary>
    public interface IDialogAware : INavigationAware
    {
        /// <summary>
        /// 当Dialog请求关闭时调用，可传递参数
        /// </summary>
        Action<DialogParameters?>? RequestClose { get; set; }

        ///// <summary>
        ///// 当Dialog导航完成时的回调方法
        ///// </summary>
        ///// <param name="parameters">导航参数</param>
        //void OnNavigated(DialogParameters? parameters);
    }
}
