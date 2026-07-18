using System.Windows;

namespace SimpleNavigation.Interface.Managers
{
    /// <summary>
    /// Dialog 管理对象
    /// </summary>
    public interface IDialogManager
    {
        /// <summary>
        /// 获取或创建指定类型的对话框窗口实例
        /// </summary>
        /// <param name="windowType">对话框窗口类型</param>
        /// <returns>对话框窗口实例</returns>
        Window GetOrCreateWindow(Type windowType);

        /// <summary>
        /// 获取已创建且仍存活的指定类型对话框窗口实例
        /// </summary>
        /// <param name="windowType">对话框窗口类型</param>
        /// <returns>现有窗口实例；如果不存在则返回 <see langword="null"/></returns>
        Window? GetExistingWindow(Type windowType);

        /// <summary>
        /// 获取指定类型的对话框窗口实例
        /// </summary>
        /// <typeparam name="T">对话框窗口类型</typeparam>
        /// <returns>对话框窗口实例</returns>
        public T? GetDialogWindow<T>() where T : Window;
    }
}
