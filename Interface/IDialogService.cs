using SimpleNavigation.Common;
using System.Windows;

namespace SimpleNavigation.Interface
{
    /// <summary>
    /// 窗口服务接口，用于打开新窗口
    /// </summary>
    public interface IDialogService
    {
        public void Show<T>(DialogParameters? parameters = null) where T : Window;

        public DialogParameters? ShowDialog<T>(DialogParameters? parameters = null) where T : Window;
    }
}
