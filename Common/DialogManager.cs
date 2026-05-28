using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Interface;

namespace SimpleNavigation.Common
{
    public class DialogManager : IDialogManager
    {
        private readonly IServiceProvider provider;
        private Dictionary<Type, WeakReference<Window>> dialogWindows = new();

        public DialogManager(IServiceProvider provider)
        {
            this.provider = provider;
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (sender == null) return;

            var type = sender.GetType();
            if (dialogWindows.ContainsKey(type))
                dialogWindows.Remove(type);
        }

        public T? GetDialogWindow<T>() where T : Window
        {
            if (dialogWindows.ContainsKey(typeof(T)))
                return dialogWindows[typeof(T)].TryGetTarget(out var window) ? window as T : null;

            var weakWindow = new WeakReference<Window>(provider.GetRequiredService<T>());
            dialogWindows[typeof(T)] = weakWindow;

            var getResult = weakWindow.TryGetTarget(out var w);
            if (!getResult || w == null) return null;

            w.Closed += OnWindowClosed;

            return w as T;
        }
    }
}
