using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface.Managers;
using SimpleNavigation.Interface.Services;
using SimpleNavigation.Services;
using SimpleNavigation.Tests.TestInfrastructure;

namespace SimpleNavigation.Tests;

public sealed class DialogServiceTests
{
    [Fact]
    public void Show_GenericTypeAndKey_ResolveOrdinaryDiAndPresentWindows()
    {
        StaTest.Run(() =>
        {
            var firstCount = 0;
            var secondCount = 0;
            FirstWindow? currentFirst = null;
            SecondWindow? currentSecond = null;
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<FirstWindow>(_ =>
            {
                firstCount++;
                currentFirst = new FirstWindow();
                return currentFirst;
            });
            services.AddTransient<SecondWindow>(_ =>
            {
                secondCount++;
                currentSecond = new SecondWindow();
                return currentSecond;
            });
            services.AddWindow<SecondWindow>("second");
            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IDialogService>();

            service.Show<FirstWindow>();
            Assert.NotNull(currentFirst);
            Assert.True(currentFirst!.IsVisible);
            currentFirst.Close();

            service.Show(typeof(FirstWindow));
            Assert.NotNull(currentFirst);
            Assert.True(currentFirst!.IsVisible);
            currentFirst.Close();

            service.Show("second");

            Assert.NotNull(currentSecond);
            Assert.True(currentSecond!.IsVisible);
            Assert.Equal(2, firstCount);
            Assert.Equal(1, secondCount);
            currentSecond.Close();
        });
    }

    [Fact]
    public void Show_KeyIsCaseSensitiveAndUnknownKeyFailsBeforeDiResolution()
    {
        StaTest.Run(() =>
        {
            var resolutionCount = 0;
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddWindow<FirstWindow>("main");
            services.AddTransient<FirstWindow>(_ =>
            {
                resolutionCount++;
                return new FirstWindow();
            });
            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IDialogService>();

            Assert.Throws<KeyNotFoundException>(() => service.Show("Main"));
            Assert.Throws<KeyNotFoundException>(() => service.Show("missing"));
            Assert.Equal(0, resolutionCount);
        });
    }

    [Fact]
    public void Show_ReusesVisibleWindowThenCreatesNewTransientAfterClosed()
    {
        StaTest.Run(() =>
        {
            var instances = new List<ReuseWindow>();
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<ReuseWindow>(_ =>
            {
                var window = new ReuseWindow();
                instances.Add(window);
                return window;
            });
            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IDialogService>();

            service.Show<ReuseWindow>();
            service.Show(typeof(ReuseWindow));

            Assert.Single(instances);
            Assert.True(instances[0].IsVisible);
            instances[0].Close();

            service.Show<ReuseWindow>();

            Assert.Equal(2, instances.Count);
            Assert.NotSame(instances[0], instances[1]);
            instances[1].Close();
        });
    }

    [Fact]
    public void Show_NotifiesWindowThenDistinctDataContextAndDeduplicatesSameReference()
    {
        StaTest.Run(() =>
        {
            var calls = new List<string>();
            var viewModel = new AwareDialogViewModel(calls);
            var window = new AwareWindow(calls) { DataContext = viewModel };
            var parameters = new DialogParameters("answer", 42);
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();

            service.Show<AwareWindow>(parameters);

            Assert.Equal(new[] { "window", "view-model" }, calls);
            Assert.Same(parameters, window.Parameters);
            Assert.Same(parameters, viewModel.Parameters);
            Assert.NotNull(window.RequestClose);
            Assert.NotNull(viewModel.RequestClose);
            viewModel.RequestClose!(null);
            Assert.False(window.IsVisible);
            Assert.Null(window.RequestClose);
            Assert.Null(viewModel.RequestClose);

            var selfContext = new AwareWindow { DataContext = null };
            selfContext.DataContext = selfContext;
            using var selfProvider = BuildProvider(services => services.AddSingleton(selfContext));
            selfProvider.GetRequiredService<IDialogService>().Show<AwareWindow>();

            Assert.Equal(1, selfContext.CallCount);
            Assert.Null(selfContext.Parameters);
            selfContext.Close();
        });
    }

    [Fact]
    public void Show_AwarenessExceptionRollsBackNewSubscriptionImmediately()
    {
        StaTest.Run(() =>
        {
            var viewModel = new ThrowingAwareDialogViewModel();
            var window = new AwareWindow { DataContext = viewModel };
            using var provider = BuildProvider(services => services.AddSingleton(window));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IDialogService>().Show<AwareWindow>());

            Assert.Equal("dialog awareness failed", exception.Message);
            Assert.False(window.IsVisible);
            Assert.Null(window.RequestClose);
            Assert.Null(viewModel.RequestClose);
            Assert.Equal(0, GetNonModalSubscriptionCount(provider));
        });
    }

    [Fact]
    public void Show_AwarenessFailureRestoresPriorLiveSubscription()
    {
        StaTest.Run(() =>
        {
            var viewModel = new ToggleThrowAwareDialogViewModel();
            var window = new AwareWindow { DataContext = viewModel };
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            service.Show<AwareWindow>();
            var windowCallback = window.RequestClose;
            var viewModelCallback = viewModel.RequestClose;
            viewModel.ThrowOnNavigated = true;

            Assert.Throws<InvalidOperationException>(() => service.Show<AwareWindow>());

            Assert.Same(windowCallback, window.RequestClose);
            Assert.Same(viewModelCallback, viewModel.RequestClose);
            Assert.Equal(1, GetNonModalSubscriptionCount(provider));
            windowCallback!(null);
            Assert.Equal(0, GetNonModalSubscriptionCount(provider));
        });
    }

    [Fact]
    public void Show_ReentrantSuccessfulReplacementSurvivesOuterRollback()
    {
        StaTest.Run(() =>
        {
            var viewModel = new ThrowingAwareDialogViewModel();
            var window = new OneShotReentrantAwareWindow();
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            service.Show<OneShotReentrantAwareWindow>();
            window.Hide();

            Action<DialogParameters?>? replacementCallback = null;
            window.DataContext = viewModel;
            window.NavigatedAction = () =>
            {
                window.DataContext = null;
                service.Show<OneShotReentrantAwareWindow>();
                replacementCallback = window.RequestClose;
                window.DataContext = viewModel;
            };

            Assert.Throws<InvalidOperationException>(() =>
                service.Show<OneShotReentrantAwareWindow>());

            Assert.NotNull(replacementCallback);
            Assert.Same(replacementCallback, window.RequestClose);
            Assert.Null(viewModel.RequestClose);
            Assert.Equal(1, GetNonModalSubscriptionCount(provider));
            replacementCallback!(null);
            Assert.Equal(0, GetNonModalSubscriptionCount(provider));
        });
    }

    [Fact]
    public void Show_PresentationFailureRollsBackNewSubscriptionImmediately()
    {
        StaTest.Run(() =>
        {
            var viewModel = new AwareDialogViewModel();
            var window = new ThrowingPresentationWindow { DataContext = viewModel };
            using var provider = BuildProvider(services => services.AddSingleton(window));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IDialogService>().Show<ThrowingPresentationWindow>());

            Assert.Equal("window presentation failed", exception.Message);
            Assert.Null(window.RequestClose);
            Assert.Null(viewModel.RequestClose);
            Assert.Equal(0, GetNonModalSubscriptionCount(provider));
        });
    }

    [Fact]
    public void Show_SynchronousRequestCloseReturnsWithoutPresentingClosedWindow()
    {
        StaTest.Run(() =>
        {
            var window = new SynchronousCloseAwareWindow();
            using var provider = BuildProvider(services => services.AddSingleton(window));

            provider.GetRequiredService<IDialogService>().Show<SynchronousCloseAwareWindow>();

            Assert.False(window.IsVisible);
            Assert.Null(provider.GetRequiredService<IDialogManager>()
                .GetExistingWindow(typeof(SynchronousCloseAwareWindow)));
            Assert.Equal(0, GetNonModalSubscriptionCount(provider));
        });
    }

    [Fact]
    public void Show_SynchronousCancelledRequestCloseContinuesPresentation()
    {
        StaTest.Run(() =>
        {
            var window = new CancelFirstCloseAwareWindow
            {
                RequestCloseOnNavigated = true,
            };
            using var provider = BuildProvider(services => services.AddSingleton(window));

            provider.GetRequiredService<IDialogService>().Show<CancelFirstCloseAwareWindow>();

            Assert.True(window.IsVisible);
            Assert.Equal(1, window.ClosingCount);
            window.Close();
        });
    }

    [Fact]
    public void ShowDialog_AllOverloadsCaptureRequestCloseResultAndCleanupAwareness()
    {
        StaTest.Run(() =>
        {
            var results = new[]
            {
                new DialogParameters("path", "generic"),
                new DialogParameters("path", "type"),
                new DialogParameters("path", "key"),
            };
            var windows = new List<AwareWindow>();
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddWindow<AwareWindow>("aware");
            services.AddTransient<AwareWindow>(_ =>
            {
                var viewModel = new AwareDialogViewModel();
                var window = new AwareWindow { DataContext = viewModel };
                var result = results[windows.Count];
                var invokeViewModel = windows.Count == 1;
                windows.Add(window);
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        if (invokeViewModel)
                            viewModel.RequestClose!(result);
                        else
                            window.RequestClose!(result);
                    }));
                return window;
            });
            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IDialogService>();

            Assert.Same(results[0], service.ShowDialog<AwareWindow>());
            Assert.Same(results[1], service.ShowDialog(typeof(AwareWindow)));
            Assert.Same(results[2], service.ShowDialog("aware"));

            Assert.All(windows, window =>
            {
                Assert.Null(window.RequestClose);
                Assert.Null(((AwareDialogViewModel)window.DataContext).RequestClose);
            });
        });
    }

    [Fact]
    public void ShowDialog_DirectDispatcherCloseReturnsNullAndDoesNotCancelClosing()
    {
        StaTest.Run(() =>
        {
            AwareWindow? window = null;
            using var provider = BuildProvider(services => services.AddTransient<AwareWindow>(_ =>
            {
                window = new AwareWindow { DataContext = new AwareDialogViewModel() };
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(window.Close));
                return window;
            }));

            var result = provider.GetRequiredService<IDialogService>().ShowDialog<AwareWindow>();

            Assert.Null(result);
            Assert.NotNull(window);
            Assert.Null(window!.RequestClose);
            Assert.Null(((AwareDialogViewModel)window.DataContext).RequestClose);
        });
    }

    [Fact]
    public void ShowDialog_AwarenessExceptionPropagatesAndCleansBothCallbacks()
    {
        StaTest.Run(() =>
        {
            var viewModel = new ThrowingAwareDialogViewModel();
            var window = new AwareWindow { DataContext = viewModel };
            using var provider = BuildProvider(services => services.AddSingleton(window));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IDialogService>().ShowDialog<AwareWindow>());

            Assert.Equal("dialog awareness failed", exception.Message);
            Assert.Null(window.RequestClose);
            Assert.Null(viewModel.RequestClose);
            window.Close();
        });
    }

    [Fact]
    public void ShowDialog_VisibleNonModalWindowFailsBeforeReplacingCallbacks()
    {
        StaTest.Run(() =>
        {
            var viewModel = new AwareDialogViewModel();
            var window = new AwareWindow { DataContext = viewModel };
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            service.Show<AwareWindow>();
            var windowCallback = window.RequestClose;
            var viewModelCallback = viewModel.RequestClose;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.ShowDialog<AwareWindow>());

            Assert.Contains("visible", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Same(windowCallback, window.RequestClose);
            Assert.Same(viewModelCallback, viewModel.RequestClose);
            windowCallback!(null);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void ShowDialog_ReentrantShowFailsWithoutReplacingModalCallback()
    {
        StaTest.Run(() =>
        {
            var result = new DialogParameters("result", 42);
            var window = new OneShotReentrantAwareWindow();
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            Exception? reentrantFailure = null;
            var callbackWasPreserved = false;
            window.NavigatedAction = () =>
            {
                var modalCallback = window.RequestClose;
                try
                {
                    service.Show<OneShotReentrantAwareWindow>();
                }
                catch (Exception exception)
                {
                    reentrantFailure = exception;
                }

                callbackWasPreserved = ReferenceEquals(modalCallback, window.RequestClose);
                if (reentrantFailure != null)
                {
                    window.Dispatcher.BeginInvoke(
                        DispatcherPriority.Normal,
                        new Action(() => window.RequestClose!(result)));
                }
            };

            DialogParameters? actual = null;
            try
            {
                actual = service.ShowDialog<OneShotReentrantAwareWindow>();
            }
            finally
            {
                if (window.IsVisible)
                    window.Close();
            }

            var exception = Assert.IsType<InvalidOperationException>(reentrantFailure);
            Assert.Contains("modal", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(callbackWasPreserved);
            Assert.Same(result, actual);
            Assert.Null(window.RequestClose);
        });
    }

    [Fact]
    public void ShowDialog_NestedSameWindowFailsBeforeReplacingOuterCallback()
    {
        StaTest.Run(() =>
        {
            var window = new OneShotReentrantAwareWindow();
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            Exception? nestedFailure = null;
            var callbackWasPreserved = false;
            window.NavigatedAction = () =>
            {
                var outerCallback = window.RequestClose;
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(window.Close));
                try
                {
                    service.ShowDialog<OneShotReentrantAwareWindow>();
                }
                catch (Exception exception)
                {
                    nestedFailure = exception;
                }

                callbackWasPreserved = ReferenceEquals(outerCallback, window.RequestClose);
            };

            Assert.Null(service.ShowDialog<OneShotReentrantAwareWindow>());

            var exception = Assert.IsType<InvalidOperationException>(nestedFailure);
            Assert.Contains("modal", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(callbackWasPreserved);
            Assert.Null(window.RequestClose);
        });
    }

    [Fact]
    public void ShowDialog_SynchronousRequestCloseReturnsCommittedResultWithoutPresentation()
    {
        StaTest.Run(() =>
        {
            var result = new DialogParameters("result", 42);
            var window = new SynchronousCloseAwareWindow { CloseResult = result };
            using var provider = BuildProvider(services => services.AddSingleton(window));

            var actual = provider.GetRequiredService<IDialogService>()
                .ShowDialog<SynchronousCloseAwareWindow>();

            Assert.Same(result, actual);
            Assert.False(window.IsVisible);
            Assert.Null(provider.GetRequiredService<IDialogManager>()
                .GetExistingWindow(typeof(SynchronousCloseAwareWindow)));
        });
    }

    [Fact]
    public void ShowDialog_CancelledRequestCloseDoesNotCommitResultAndDirectCloseReturnsNull()
    {
        StaTest.Run(() =>
        {
            var candidate = new DialogParameters("candidate", 1);
            CancelFirstCloseAwareWindow? window = null;
            using var provider = BuildProvider(services =>
                services.AddTransient<CancelFirstCloseAwareWindow>(_ =>
                {
                    window = new CancelFirstCloseAwareWindow { CloseResult = candidate };
                    window.Dispatcher.BeginInvoke(
                        DispatcherPriority.Normal,
                        new Action(() => window.RequestClose!(candidate)));
                    window.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(window.Close));
                    return window;
                }));

            var actual = provider.GetRequiredService<IDialogService>()
                .ShowDialog<CancelFirstCloseAwareWindow>();

            Assert.Null(actual);
            Assert.NotNull(window);
            Assert.Equal(2, window!.ClosingCount);
        });
    }

    [Fact]
    public void ShowDialog_SynchronousCancelledRequestCloseContinuesModalPresentation()
    {
        StaTest.Run(() =>
        {
            var candidate = new DialogParameters("candidate", 1);
            var window = new CancelFirstCloseAwareWindow
            {
                RequestCloseOnNavigated = true,
                CloseResult = candidate,
            };
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(window.Close));
            using var provider = BuildProvider(services => services.AddSingleton(window));

            var actual = provider.GetRequiredService<IDialogService>()
                .ShowDialog<CancelFirstCloseAwareWindow>();

            Assert.Null(actual);
            Assert.Equal(2, window.ClosingCount);
        });
    }

    [Fact]
    public void ShowDialog_FinallyDoesNotClearAnUnrelatedCallbackReplacement()
    {
        StaTest.Run(() =>
        {
            var viewModel = new ReplacingAwareDialogViewModel();
            AwareWindow? window = null;
            using var provider = BuildProvider(services => services.AddTransient<AwareWindow>(_ =>
            {
                window = new AwareWindow { DataContext = viewModel };
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(window.Close));
                return window;
            }));

            Assert.Null(provider.GetRequiredService<IDialogService>().ShowDialog<AwareWindow>());

            Assert.NotNull(window);
            Assert.Null(window!.RequestClose);
            Assert.Same(viewModel.Replacement, viewModel.RequestClose);
        });
    }

    [Fact]
    public void Close_GenericTypeAndKey_CloseExistingWindows()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<FirstWindow>();
            services.AddWindow<SecondWindow>("second");
            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IDialogService>();

            service.Show<FirstWindow>();
            Assert.True(service.Close<FirstWindow>());
            service.Show(typeof(FirstWindow));
            Assert.True(service.Close(typeof(FirstWindow)));
            service.Show("second");
            Assert.True(service.Close("second"));
        });
    }

    [Fact]
    public void Close_UnopenedWindowReturnsFalseWithoutInvokingDiFactory()
    {
        StaTest.Run(() =>
        {
            var resolutionCount = 0;
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddWindow<FirstWindow>("first");
            services.AddTransient<FirstWindow>(_ =>
            {
                resolutionCount++;
                return new FirstWindow();
            });
            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IDialogService>();

            Assert.False(service.Close<FirstWindow>());
            Assert.False(service.Close(typeof(FirstWindow)));
            Assert.False(service.Close("first"));
            Assert.Equal(0, resolutionCount);
        });
    }

    [Fact]
    public void Close_CancelledClosingReturnsFalseAndWindowRemainsManaged()
    {
        StaTest.Run(() =>
        {
            var window = new CancelClosingWindow { CancelClosing = true };
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            service.Show<CancelClosingWindow>();

            Assert.False(service.Close<CancelClosingWindow>());
            Assert.True(window.IsVisible);

            window.CancelClosing = false;
            Assert.True(service.Close<CancelClosingWindow>());
        });
    }

    [Fact]
    public void Close_OpenNonFocusedWindowStillCloses()
    {
        StaTest.Run(() =>
        {
            var window = new FirstWindow();
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            service.Show<FirstWindow>();
            var activeWindow = new SecondWindow();
            activeWindow.Show();
            activeWindow.Activate();
            StaTest.PumpDispatcher();

            Assert.False(window.IsActive);
            Assert.True(service.Close<FirstWindow>());
            activeWindow.Close();
        });
    }

    [Fact]
    public void Close_RequiresOwningWindowDispatcherThread()
    {
        StaTest.Run(() =>
        {
            var window = new FirstWindow();
            using var provider = BuildProvider(services => services.AddSingleton(window));
            var service = provider.GetRequiredService<IDialogService>();
            service.Show<FirstWindow>();
            Exception? failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    service.Close<FirstWindow>();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));

            Assert.IsType<InvalidOperationException>(failure);
            Assert.True(service.Close<FirstWindow>());
        });
    }

    [Fact]
    public void ShowAndShowDialog_RequireOwningWindowDispatcherThread()
    {
        StaTest.Run(() =>
        {
            var window = new FirstWindow();
            using var provider = BuildProvider(services => services.AddSingleton(window));
            provider.GetRequiredService<IDialogManager>()
                .GetOrCreateWindow(typeof(FirstWindow));
            var service = provider.GetRequiredService<IDialogService>();
            Exception? showFailure = null;
            Exception? modalFailure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    service.Show<FirstWindow>();
                }
                catch (Exception exception)
                {
                    showFailure = exception;
                }

                try
                {
                    service.ShowDialog<FirstWindow>();
                }
                catch (Exception exception)
                {
                    modalFailure = exception;
                }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));

            Assert.IsType<InvalidOperationException>(showFailure);
            Assert.IsType<InvalidOperationException>(modalFailure);
            window.Close();
        });
    }

    [Fact]
    public void InvalidTargetsAndKeysFailFastAndMissingDiExceptionIsPreserved()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IDialogService>();

        Assert.Equal("targetType", Assert.Throws<ArgumentNullException>(
            () => service.Show((Type)null!)).ParamName);
        Assert.Equal("targetType", Assert.Throws<ArgumentNullException>(
            () => service.ShowDialog((Type)null!)).ParamName);
        Assert.Equal("targetType", Assert.Throws<ArgumentNullException>(
            () => service.Close((Type)null!)).ParamName);
        Assert.Equal("targetType", Assert.Throws<ArgumentException>(
            () => service.Show(typeof(string))).ParamName);
        Assert.Equal("targetType", Assert.Throws<ArgumentException>(
            () => service.ShowDialog(typeof(string))).ParamName);
        Assert.Equal("targetType", Assert.Throws<ArgumentException>(
            () => service.Close(typeof(string))).ParamName);
        Assert.Throws<ArgumentException>(() => service.Show(" "));
        Assert.Throws<KeyNotFoundException>(() => service.Close("missing"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.Show(typeof(FirstWindow)));
        Assert.Contains(typeof(FirstWindow).ToString(), exception.Message);
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static int GetNonModalSubscriptionCount(IServiceProvider provider)
    {
        var service = Assert.IsType<DialogService>(
            provider.GetRequiredService<IDialogService>());
        var field = typeof(DialogService).GetField(
            "nonModalSubscriptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var subscriptions = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            field!.GetValue(service));
        return subscriptions.Count;
    }
}
