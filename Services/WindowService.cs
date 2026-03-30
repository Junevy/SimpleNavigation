using System.Collections.Concurrent;
using System.Windows;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;

namespace SimpleNavigation.Services
{
    /// <summary>
    /// 窗口服务实现类
    /// </summary>
    public class WindowService : IWindowService
    {
        /// <summary>
        /// 窗口注册字典，用于存储窗口类型和ViewModel类型的映射关系
        /// </summary>
        public readonly ConcurrentDictionary<string, WindowRegistration> WindowRegistrations = new();

        /// <summary>
        /// 注册窗口类型和ViewModel类型
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <param name="windowName">窗口名称</param>
        /// <param name="factory">窗口实例创建工厂</param>
        /// <param name="options">窗口选项配置</param>
        public void Register<TWindow>(string windowName, Func<Window> factory, DialogOptions? options = null)
            where TWindow : Window, new()
        {
            if (string.IsNullOrEmpty(windowName))
                throw new ArgumentException("窗口名称不能为空");

            if (WindowRegistrations.ContainsKey(windowName))
                throw new ArgumentException("窗口名称已存在");

            // 注册Window Registration
            WindowRegistrations[windowName] 
                = new WindowRegistration(typeof(TWindow), options ?? new DialogOptions(), factory);
        }

        /// <summary>
        /// 取消注册窗口类型和ViewModel类型
        /// </summary>
        /// <param name="windowName">窗口名称</param>
        /// <returns>是否成功取消注册</returns>
        public bool UnRegister(string windowName)
        {
            return WindowRegistrations.TryRemove(windowName, out _);
        }

        /// <summary>
        /// 打开新窗口
        /// </summary>
        /// <param name="beforeWindow">打开新窗口前的窗口实例，用于新窗口关闭后传递参数</param>
        /// <param name="windowName">新窗口名称</param>
        /// <param name="parameters">新窗口携带的参数</param>
        public void OpenWindow(IWindowAware beforeWindow, string windowName, DialogParameters? parameters)
        {
            if (string.IsNullOrEmpty(windowName))
                throw new ArgumentException("窗口名称不能为空");

            if (!WindowRegistrations.TryGetValue(windowName, out var registration))
                throw new ArgumentException("窗口名称不存在");

            var window = registration.GetWindow();

            window.Closing += (s,e) =>
            {
                if (e.Cancel)
                {
                    e.Cancel = false;
                    OnWindowClosing(beforeWindow, parameters);
                }
            };

            window.Closed += (s,e) =>
            {
                OnWindowClosed(beforeWindow, parameters);
            };

            OnWindowNavigating(window, parameters);
            window.Show();
            OnWindowNavigated(window, parameters);
        }

        #region WindowAware Trigger
        /// <summary>
        /// 新窗口即将打开时的回调方法
        /// </summary>
        /// <param name="window">新窗口实例</param>
        /// <param name="parameters">新窗口携带的参数</param>
        private void OnWindowNavigating(Window window, DialogParameters? parameters)
        {
            if (window is IWindowAware wA)
                wA.OnWindowNavigating(parameters);
        }

        /// <summary>
        /// 新窗口打开完成后的回调方法
        /// </summary>
        /// <param name="window">新窗口实例</param>
        /// <param name="parameters">新窗口携带的参数</param>
        private void OnWindowNavigated(Window window, DialogParameters? parameters)
        {
            if (window is IWindowAware wA)
                wA.OnWindowNavigated(parameters);
        }

        /// <summary>
        /// 当前窗口关闭前的回调方法
        /// </summary>
        /// <param name="beforeWindow">当前窗口实例</param>
        /// <param name="parameters">当前窗口携带的参数</param>
        public void OnWindowClosed(IWindowAware beforeWindow, DialogParameters? parameters)
        {
            if (beforeWindow is IWindowAware wA)
                wA.OnWindowClosed(parameters);
        }

        /// <summary>
        /// 当前窗口关闭时的回调方法
        /// </summary>
        /// <param name="beforeWindow">当前窗口实例</param>
        /// <param name="parameters">当前窗口携带的参数</param>
        public void OnWindowClosing(IWindowAware beforeWindow, DialogParameters? parameters)
        {
            if (beforeWindow is IWindowAware wA)
                wA.OnWindowClosing(parameters);
        }
    }
    #endregion
}
