using System.Windows;

namespace SimpleNavigation.Interface.Managers
{
    /// <summary>
    /// Dialog 管理对象
    /// </summary>
    public interface IDialogManager
    {
        /// <summary>
        /// 获取指定类型的对话框窗口实例
        /// </summary>
        /// <typeparam name="T">对话框窗口类型</typeparam>
        /// <returns>对话框窗口实例</returns>
        public T? GetDialogWindow<T>() where T : Window;
    }
}
