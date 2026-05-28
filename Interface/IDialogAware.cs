using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    public interface IDialogAware
    {
        /// <summary>
        /// 当Dialog请求关闭时调用
        /// </summary>
        Action<DialogParameters?>? RequestClose { get; set; }

        /// <summary>
        /// 当Dialog导航完成时的回调方法
        /// </summary>
        /// <param name="parameters">导航参数</param>
        void OnNavigated(DialogParameters? parameters);
    }
}
