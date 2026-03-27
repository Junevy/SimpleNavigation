using System.Windows;

namespace SimpleNavigation.Common
{
    /// <summary>
    /// 窗口注册类
    /// </summary>
    public class WindowRegistration
    {
        private readonly Func<Window> factory;
        private Window? cacheWindow;

        /// <summary>
        /// 唯一标识
        /// </summary>
        public Guid Id { get; } = new Guid();

        /// <summary>
        /// 窗口类型
        /// </summary>
        public Type WindowType { get; set; } = null!;

        public DialogOptions Options { get; set; } = null!;

        public WindowRegistration(Type windowType, DialogOptions options, Func<Window> factory)
        {
            WindowType = windowType;
            Options = options;
            this.factory = factory;
        }

        public Window GetWindow()
        {
            if (Options.AllowMulti == DialogOptions.DialogMode.Singleton)
                return cacheWindow ??= factory();

            return factory();
        }
    }
    
}
