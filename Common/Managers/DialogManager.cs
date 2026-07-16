using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Interface.Managers;

namespace SimpleNavigation.Common.Managers
{
    public class DialogManager : IDialogManager
    {
        private readonly IServiceProvider provider;
        private readonly object syncRoot = new();
        private readonly Dictionary<Type, WeakReference<Window>> dialogWindows = new();

        public DialogManager(IServiceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (sender is not Window closedWindow) return;

            lock (syncRoot)
            {
                Type? matchingType = null;

                foreach (var dialogWindow in dialogWindows)
                {
                    if (dialogWindow.Value.TryGetTarget(out var cachedWindow)
                        && ReferenceEquals(cachedWindow, closedWindow))
                    {
                        matchingType = dialogWindow.Key;
                        break;
                    }
                }

                if (matchingType != null)
                {
                    dialogWindows.Remove(matchingType);
                }
            }
        }

        public Window GetOrCreateWindow(Type windowType)
        {
            ValidateWindowType(windowType);

            var existingWindow = GetExistingWindowCore(windowType);
            if (existingWindow != null)
            {
                return existingWindow;
            }

            var service = provider.GetRequiredService(windowType);
            if (service is not Window newWindow)
            {
                throw new InvalidOperationException(
                    $"The service registered for window type '{windowType.FullName}' resolved to " +
                    $"'{service.GetType().FullName}', which is not a '{typeof(Window).FullName}'.");
            }

            lock (syncRoot)
            {
                if (dialogWindows.TryGetValue(windowType, out var weakWindow))
                {
                    if (weakWindow.TryGetTarget(out existingWindow))
                    {
                        return existingWindow;
                    }

                    dialogWindows.Remove(windowType);
                }

                dialogWindows[windowType] = new WeakReference<Window>(newWindow);
            }

            newWindow.Closed += OnWindowClosed;
            return newWindow;
        }

        public Window? GetExistingWindow(Type windowType)
        {
            ValidateWindowType(windowType);
            return GetExistingWindowCore(windowType);
        }

        public T? GetDialogWindow<T>() where T : Window
        {
            return (T)GetOrCreateWindow(typeof(T));
        }

        private Window? GetExistingWindowCore(Type windowType)
        {
            lock (syncRoot)
            {
                if (!dialogWindows.TryGetValue(windowType, out var weakWindow))
                {
                    return null;
                }

                if (weakWindow.TryGetTarget(out var window))
                {
                    return window;
                }

                dialogWindows.Remove(windowType);
                return null;
            }
        }

        private static void ValidateWindowType(Type windowType)
        {
            if (windowType == null)
            {
                throw new ArgumentNullException(nameof(windowType));
            }

            if (!typeof(Window).IsAssignableFrom(windowType))
            {
                throw new ArgumentException(
                    $"Type '{windowType.FullName}' must derive from '{typeof(Window).FullName}'.",
                    nameof(windowType));
            }
        }
    }
}
