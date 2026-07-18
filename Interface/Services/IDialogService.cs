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
        /// <typeparam name="TWindow">Dialog类型</typeparam>
        /// <param name="parameters">Dialog参数</param>
        public void Show<TWindow>(DialogParameters? parameters = null) where TWindow : Window;

        public void Show(Type targetType, DialogParameters? parameters = null);

        public void Show(string key, DialogParameters? parameters = null);

        /// <summary>
        /// 显示一个新Dialog并等待用户关闭
        /// </summary>
        /// <typeparam name="TWindow">Dialog类型</typeparam>
        /// <param name="parameters">Dialog参数</param>
        /// <returns>Dialog关闭时返回的参数</returns>
        public DialogParameters? ShowDialog<TWindow>(DialogParameters? parameters = null) where TWindow : Window;

        public DialogParameters? ShowDialog(Type targetType, DialogParameters? parameters = null);

        public DialogParameters? ShowDialog(string key, DialogParameters? parameters = null);

        public bool Close<TWindow>() where TWindow : Window;

        public bool Close(Type targetType);

        public bool Close(string key);
    }
}
