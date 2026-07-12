using SimpleNavigation.Common;
using System.Windows;

namespace SimpleNavigation.Interface.Services
{
    /// <summary>
    /// 窗口服务接口，用于打开新窗口
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 显示一个新Dialog
        /// </summary>
        /// <typeparam name="T">Dialog类型</typeparam>
        /// <param name="parameters">Dialog参数</param>
        public void Show<T>(DialogParameters? parameters = null) where T : Window;

        /// <summary>
        /// 显示一个新Dialog并等待用户关闭
        /// </summary>
        /// <typeparam name="T">Dialog类型</typeparam>
        /// <param name="parameters">Dialog参数</param>
        /// <returns>Dialog关闭时返回的参数</returns>
        public DialogParameters? ShowDialog<T>(DialogParameters? parameters = null) where T : Window;
    }
}
