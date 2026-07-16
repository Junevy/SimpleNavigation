using System.Runtime.ExceptionServices;
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
        private readonly Dictionary<Type, ResolutionState> resolutionStates = new();

        public DialogManager(IServiceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (sender is not Window closedWindow) return;

            closedWindow.Closed -= OnWindowClosed;

            lock (syncRoot)
            {
                var windowType = closedWindow.GetType();
                if (dialogWindows.TryGetValue(windowType, out var weakWindow)
                    && weakWindow.TryGetTarget(out var cachedWindow)
                    && ReferenceEquals(cachedWindow, closedWindow))
                {
                    dialogWindows.Remove(windowType);
                }
            }
        }

        public Window GetOrCreateWindow(Type windowType)
        {
            ValidateWindowType(windowType);

            while (true)
            {
                ResolutionState resolutionState;
                var ownsResolution = false;

                lock (syncRoot)
                {
                    var existingWindow = GetExistingWindowLocked(windowType);
                    if (existingWindow != null)
                    {
                        return existingWindow;
                    }

                    if (resolutionStates.TryGetValue(windowType, out var activeResolutionState))
                    {
                        resolutionState = activeResolutionState;
                        if (resolutionState.OwnerThreadId == Environment.CurrentManagedThreadId)
                        {
                            throw new InvalidOperationException(
                                $"Window type '{windowType.FullName}' is already being resolved by this dialog manager.");
                        }
                    }
                    else
                    {
                        resolutionState = new ResolutionState(Environment.CurrentManagedThreadId);
                        resolutionStates.Add(windowType, resolutionState);
                        ownsResolution = true;
                    }
                }

                if (ownsResolution)
                {
                    return ResolveAndPublishWindow(windowType, resolutionState);
                }

                resolutionState.Completion.Task.GetAwaiter().GetResult();
                if (resolutionState.ResolutionException != null)
                {
                    resolutionState.ResolutionException.Throw();
                }
            }
        }

        private Window ResolveAndPublishWindow(Type windowType, ResolutionState resolutionState)
        {
            try
            {
                var service = provider.GetRequiredService(windowType);
                if (service is not Window newWindow)
                {
                    throw new InvalidOperationException(
                        $"The service registered for window type '{windowType.FullName}' resolved to " +
                        $"'{service.GetType().FullName}', which is not a '{typeof(Window).FullName}'.");
                }

                if (newWindow.GetType() != windowType)
                {
                    throw new InvalidOperationException(
                        $"The service registered for window type '{windowType.FullName}' resolved to " +
                        $"the different window type '{newWindow.GetType().FullName}'.");
                }

                newWindow.Closed += OnWindowClosed;

                lock (syncRoot)
                {
                    dialogWindows[windowType] = new WeakReference<Window>(newWindow);
                    RemoveResolutionStateLocked(windowType, resolutionState);
                }

                resolutionState.Completion.TrySetResult(true);
                return newWindow;
            }
            catch (Exception exception)
            {
                resolutionState.ResolutionException = ExceptionDispatchInfo.Capture(exception);

                lock (syncRoot)
                {
                    RemoveResolutionStateLocked(windowType, resolutionState);
                }

                resolutionState.Completion.TrySetResult(true);
                throw;
            }
        }

        private void RemoveResolutionStateLocked(Type windowType, ResolutionState resolutionState)
        {
            if (resolutionStates.TryGetValue(windowType, out var currentState)
                && ReferenceEquals(currentState, resolutionState))
            {
                resolutionStates.Remove(windowType);
            }
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
                return GetExistingWindowLocked(windowType);
            }
        }

        private Window? GetExistingWindowLocked(Type windowType)
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

        private sealed class ResolutionState
        {
            public ResolutionState(int ownerThreadId)
            {
                OwnerThreadId = ownerThreadId;
                Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public int OwnerThreadId { get; }

            public TaskCompletionSource<bool> Completion { get; }

            public ExceptionDispatchInfo? ResolutionException { get; set; }
        }
    }
}
