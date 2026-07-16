using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Interface.Awares;
using SimpleNavigation.Interface.Managers;
using SimpleNavigation.Interface.Services;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SimpleNavigation.Services
{
    /// <summary>
    /// 窗口服务实现类
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly IDialogManager dialogManager;
        private readonly NavigationRouteRegistry routes;
        private readonly object subscriptionSyncRoot = new();
        private readonly Dictionary<Window, NonModalSubscription> nonModalSubscriptions =
            new(WindowReferenceComparer.Instance);
        private readonly object modalSyncRoot = new();
        private readonly HashSet<Window> activeModalWindows =
            new(WindowReferenceComparer.Instance);

        public DialogService(IServiceProvider provider, IDialogManager dialogManager)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            this.dialogManager = dialogManager
                ?? throw new ArgumentNullException(nameof(dialogManager));
            routes = provider.GetRequiredService<NavigationRouteRegistry>();
        }

        public void Show<TWindow>(DialogParameters? parameters = null) where TWindow : Window
        {
            Show(typeof(TWindow), parameters);
        }

        public void Show(Type targetType, DialogParameters? parameters = null)
        {
            ValidateWindowType(targetType);
            ShowCore(dialogManager.GetOrCreateWindow(targetType), parameters);
        }

        public void Show(string key, DialogParameters? parameters = null)
        {
            var targetType = routes.GetRequiredDialogType(key);
            ValidateWindowType(targetType);
            ShowCore(dialogManager.GetOrCreateWindow(targetType), parameters);
        }

        public DialogParameters? ShowDialog<TWindow>(DialogParameters? parameters = null)
            where TWindow : Window
        {
            return ShowDialog(typeof(TWindow), parameters);
        }

        public DialogParameters? ShowDialog(Type targetType, DialogParameters? parameters = null)
        {
            ValidateWindowType(targetType);
            return ShowDialogCore(dialogManager.GetOrCreateWindow(targetType), parameters);
        }

        public DialogParameters? ShowDialog(string key, DialogParameters? parameters = null)
        {
            var targetType = routes.GetRequiredDialogType(key);
            ValidateWindowType(targetType);
            return ShowDialogCore(dialogManager.GetOrCreateWindow(targetType), parameters);
        }

        public bool Close<TWindow>() where TWindow : Window
        {
            return Close(typeof(TWindow));
        }

        public bool Close(Type targetType)
        {
            ValidateWindowType(targetType);
            return CloseCore(dialogManager.GetExistingWindow(targetType));
        }

        public bool Close(string key)
        {
            var targetType = routes.GetRequiredDialogType(key);
            ValidateWindowType(targetType);
            return CloseCore(dialogManager.GetExistingWindow(targetType));
        }

        private void ShowCore(Window window, DialogParameters? parameters)
        {
            window.Dispatcher.VerifyAccess();
            if (IsModalActive(window))
            {
                throw new InvalidOperationException(
                    "Show cannot replace an active modal presentation for the same window.");
            }

            var priorSubscription = RemoveNonModalSubscription(window);
            var subscription = CreateNonModalSubscription(window);

            try
            {
                AttachNonModalSubscription(window, subscription);
                NotifyAwareTargets(subscription.AwareTargets, parameters);

                if (!IsCurrentWindow(window))
                    return;

                if (!window.IsVisible)
                    window.Show();

                window.Activate();
            }
            catch
            {
                var removedSubscription =
                    RemoveNonModalSubscription(window, subscription);
                if (removedSubscription
                    && priorSubscription != null
                    && IsCurrentWindow(window))
                {
                    TryRestoreNonModalSubscription(
                        window,
                        priorSubscription,
                        subscription.RequestClose);
                }

                throw;
            }
        }

        private DialogParameters? ShowDialogCore(Window window, DialogParameters? parameters)
        {
            window.Dispatcher.VerifyAccess();
            ClaimModalOwnership(window);

            try
            {
                if (window.IsVisible)
                {
                    throw new InvalidOperationException(
                        "ShowDialog cannot be called for a visible window.");
                }

                return ShowDialogTransaction(window, parameters);
            }
            finally
            {
                ReleaseModalOwnership(window);
            }
        }

        private DialogParameters? ShowDialogTransaction(
            Window window,
            DialogParameters? parameters)
        {
            var priorSubscription = RemoveNonModalSubscription(window);

            DialogParameters? result = null;
            var awareTargets = GetAwareTargets(window);
            Action<DialogParameters?> requestClose = closeResult =>
            {
                window.Dispatcher.VerifyAccess();
                EventHandler? closedHandler = null;
                closedHandler = (_, _) => result = closeResult;
                window.Closed += closedHandler;

                try
                {
                    window.Close();
                }
                finally
                {
                    window.Closed -= closedHandler;
                }
            };

            try
            {
                SetRequestClose(awareTargets, requestClose);
                NotifyAwareTargets(awareTargets, parameters);

                if (!IsCurrentWindow(window))
                    return result;

                window.ShowDialog();
                return result;
            }
            catch
            {
                ClearRequestClose(awareTargets, requestClose);
                if (priorSubscription != null && IsCurrentWindow(window))
                {
                    TryRestoreNonModalSubscription(
                        window,
                        priorSubscription,
                        requestClose);
                }

                throw;
            }
            finally
            {
                ClearRequestClose(awareTargets, requestClose);
            }
        }

        private bool CloseCore(Window? window)
        {
            if (window == null)
                return false;

            window.Dispatcher.VerifyAccess();
            var closed = false;
            EventHandler closedHandler = (_, _) => closed = true;
            window.Closed += closedHandler;

            try
            {
                window.Close();
                return closed;
            }
            finally
            {
                window.Closed -= closedHandler;
            }
        }

        private NonModalSubscription CreateNonModalSubscription(Window window)
        {
            var awareTargets = GetAwareTargets(window);
            Action<DialogParameters?> requestClose = _ =>
            {
                window.Dispatcher.VerifyAccess();
                window.Close();
            };
            NonModalSubscription? subscription = null;
            EventHandler closedHandler = (_, _) =>
            {
                if (subscription != null)
                    RemoveNonModalSubscription(window, subscription);
            };
            subscription = new NonModalSubscription(awareTargets, requestClose, closedHandler);
            return subscription;
        }

        private void AttachNonModalSubscription(
            Window window,
            NonModalSubscription subscription)
        {
            lock (subscriptionSyncRoot)
            {
                nonModalSubscriptions.Add(window, subscription);
            }

            window.Closed += subscription.ClosedHandler;
            SetRequestClose(subscription.AwareTargets, subscription.RequestClose);
        }

        private bool TryRestoreNonModalSubscription(
            Window window,
            NonModalSubscription subscription,
            Action<DialogParameters?> failedRequestClose)
        {
            lock (subscriptionSyncRoot)
            {
                if (nonModalSubscriptions.ContainsKey(window))
                    return false;

                nonModalSubscriptions.Add(window, subscription);
            }

            try
            {
                window.Closed += subscription.ClosedHandler;
                RestoreRequestClose(
                    subscription.AwareTargets,
                    subscription.RequestClose,
                    failedRequestClose);
                return true;
            }
            catch
            {
                RemoveNonModalSubscription(window, subscription);
                return false;
            }
        }

        private NonModalSubscription? RemoveNonModalSubscription(Window window)
        {
            NonModalSubscription? subscription;
            lock (subscriptionSyncRoot)
            {
                if (!nonModalSubscriptions.TryGetValue(window, out subscription))
                    return null;

                nonModalSubscriptions.Remove(window);
            }

            window.Closed -= subscription.ClosedHandler;
            ClearRequestClose(subscription.AwareTargets, subscription.RequestClose);
            return subscription;
        }

        private bool RemoveNonModalSubscription(
            Window window,
            NonModalSubscription expectedSubscription)
        {
            lock (subscriptionSyncRoot)
            {
                if (!nonModalSubscriptions.TryGetValue(window, out var currentSubscription)
                    || !ReferenceEquals(currentSubscription, expectedSubscription))
                {
                    return false;
                }

                nonModalSubscriptions.Remove(window);
            }

            window.Closed -= expectedSubscription.ClosedHandler;
            ClearRequestClose(expectedSubscription.AwareTargets, expectedSubscription.RequestClose);
            return true;
        }

        private bool IsModalActive(Window window)
        {
            lock (modalSyncRoot)
            {
                return activeModalWindows.Contains(window);
            }
        }

        private void ClaimModalOwnership(Window window)
        {
            lock (modalSyncRoot)
            {
                if (!activeModalWindows.Add(window))
                {
                    throw new InvalidOperationException(
                        "A modal presentation is already active for this window.");
                }
            }
        }

        private void ReleaseModalOwnership(Window window)
        {
            lock (modalSyncRoot)
            {
                activeModalWindows.Remove(window);
            }
        }

        private bool IsCurrentWindow(Window window)
        {
            return ReferenceEquals(
                dialogManager.GetExistingWindow(window.GetType()),
                window);
        }

        private static IDialogAware[] GetAwareTargets(Window window)
        {
            var windowAware = window as IDialogAware;
            var dataContextAware = window.DataContext as IDialogAware;

            if (windowAware == null)
                return dataContextAware == null ? Array.Empty<IDialogAware>() : new[] { dataContextAware };

            if (dataContextAware == null || ReferenceEquals(windowAware, dataContextAware))
                return new[] { windowAware };

            return new[] { windowAware, dataContextAware };
        }

        private static void SetRequestClose(
            IEnumerable<IDialogAware> awareTargets,
            Action<DialogParameters?> requestClose)
        {
            foreach (var aware in awareTargets)
                aware.RequestClose = requestClose;
        }

        private static void NotifyAwareTargets(
            IEnumerable<IDialogAware> awareTargets,
            DialogParameters? parameters)
        {
            foreach (var aware in awareTargets)
                aware.OnNavigated(parameters);
        }

        private static void RestoreRequestClose(
            IEnumerable<IDialogAware> awareTargets,
            Action<DialogParameters?> requestClose,
            Action<DialogParameters?> failedRequestClose)
        {
            foreach (var aware in awareTargets)
            {
                var currentRequestClose = aware.RequestClose;
                if (currentRequestClose == null
                    || ReferenceEquals(currentRequestClose, failedRequestClose))
                {
                    aware.RequestClose = requestClose;
                }
            }
        }

        private static void ClearRequestClose(
            IEnumerable<IDialogAware> awareTargets,
            Action<DialogParameters?> requestClose)
        {
            foreach (var aware in awareTargets)
            {
                try
                {
                    if (ReferenceEquals(aware.RequestClose, requestClose))
                        aware.RequestClose = null;
                }
                catch
                {
                    // Cleanup must not replace the exception from the dialog operation.
                }
            }
        }

        private static void ValidateWindowType(Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (!typeof(Window).IsAssignableFrom(targetType))
            {
                throw new ArgumentException(
                    $"Target type '{targetType.FullName}' must derive from Window.",
                    nameof(targetType));
            }
        }

        private sealed class NonModalSubscription
        {
            public NonModalSubscription(
                IDialogAware[] awareTargets,
                Action<DialogParameters?> requestClose,
                EventHandler closedHandler)
            {
                AwareTargets = awareTargets;
                RequestClose = requestClose;
                ClosedHandler = closedHandler;
            }

            public IDialogAware[] AwareTargets { get; }

            public Action<DialogParameters?> RequestClose { get; }

            public EventHandler ClosedHandler { get; }
        }

        private sealed class WindowReferenceComparer : IEqualityComparer<Window>
        {
            public static WindowReferenceComparer Instance { get; } = new();

            public bool Equals(Window? x, Window? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(Window obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
