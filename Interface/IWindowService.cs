using System.Windows;
using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    /// <summary>
    /// 窗口服务接口，用于打开新窗口
    /// </summary>
    public interface IWindowService
    {
        /// <summary>
        /// 打开新窗口
        /// </summary>
        /// <param name="beforeWindow">打开新窗口前的窗口实例</param>
        /// <param name="windowName">新窗口名称</param>
        /// <param name="parameters">新窗口携带的参数</param>
        void OpenWindow(IWindowAware beforeWindow, string windowName, DialogParameters? parameters);

        // /// <summary>
        // /// 关闭当前窗口
        // /// </summary>
        // void CloseWindow(IWindowAware beforeWindow, string windowName, DialogParameters? parameters);

        /// <summary>
        /// 注册窗口类型和ViewModel类型
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <param name="windowName">窗口名称</param>
        /// <param name="factory">窗口实例创建工厂</param>
        /// <param name="options">窗口选项</param>
        void Register<TWindow>(string windowName, Func<Window> factory, DialogOptions options) where TWindow : Window, new();


        /// <summary>
        /// 取消注册窗口类型和ViewModel类型
        /// </summary>
        /// <param name="windowName">窗口名称</param>
        /// <returns>是否成功取消注册</returns>
        bool UnRegister(string windowName);

    }
}
