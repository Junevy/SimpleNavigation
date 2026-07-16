using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common.Managers;
using SimpleNavigation.Tests.TestInfrastructure;

namespace SimpleNavigation.Tests;

public sealed class DialogManagerTests
{
    [Fact]
    public void GetExistingWindow_BeforeCreation_ReturnsNullWithoutResolvingService()
    {
        StaTest.Run(() =>
        {
            var resolutionCount = 0;
            using var provider = new ServiceCollection()
                .AddTransient<ManagedWindow>(_ =>
                {
                    resolutionCount++;
                    return new ManagedWindow();
                })
                .BuildServiceProvider();
            var manager = new DialogManager(provider);

            Assert.Null(manager.GetExistingWindow(typeof(ManagedWindow)));
            Assert.Equal(0, resolutionCount);
        });
    }

    [Fact]
    public void GetOrCreateWindow_LiveWindow_ResolvesOnceAndReusesExactInstance()
    {
        StaTest.Run(() =>
        {
            var resolutionCount = 0;
            using var provider = new ServiceCollection()
                .AddTransient<ManagedWindow>(_ =>
                {
                    resolutionCount++;
                    return new ManagedWindow();
                })
                .BuildServiceProvider();
            var manager = new DialogManager(provider);

            var first = manager.GetOrCreateWindow(typeof(ManagedWindow));
            var second = manager.GetOrCreateWindow(typeof(ManagedWindow));

            Assert.IsType<ManagedWindow>(first);
            Assert.Same(first, second);
            Assert.Same(first, manager.GetExistingWindow(typeof(ManagedWindow)));
            Assert.Same(first, manager.GetDialogWindow<ManagedWindow>());
            Assert.Equal(1, resolutionCount);

            first.Close();
        });
    }

    [Fact]
    public void GetOrCreateWindow_AfterShownWindowCloses_ResolvesNewTransientInstance()
    {
        StaTest.Run(() =>
        {
            var resolutionCount = 0;
            using var provider = new ServiceCollection()
                .AddTransient<ManagedWindow>(_ =>
                {
                    resolutionCount++;
                    return new ManagedWindow();
                })
                .BuildServiceProvider();
            var manager = new DialogManager(provider);
            var first = manager.GetOrCreateWindow(typeof(ManagedWindow));

            first.Show();
            StaTest.PumpDispatcher();
            first.Close();
            StaTest.PumpDispatcher();

            Assert.Null(manager.GetExistingWindow(typeof(ManagedWindow)));

            var second = manager.GetOrCreateWindow(typeof(ManagedWindow));

            Assert.NotSame(first, second);
            Assert.Equal(2, resolutionCount);
            second.Close();
        });
    }

    [Fact]
    public void ClosedNotification_FromOlderWindow_DoesNotRemoveNewerReplacement()
    {
        StaTest.Run(() =>
        {
            using var provider = new ServiceCollection()
                .AddTransient<ManagedWindow>()
                .BuildServiceProvider();
            var manager = new DialogManager(provider);
            var first = manager.GetOrCreateWindow(typeof(ManagedWindow));

            first.Show();
            StaTest.PumpDispatcher();
            first.Close();
            StaTest.PumpDispatcher();
            var replacement = manager.GetOrCreateWindow(typeof(ManagedWindow));

            var closeCallback = typeof(DialogManager).GetMethod(
                "OnWindowClosed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(closeCallback);
            closeCallback!.Invoke(manager, new object?[] { first, EventArgs.Empty });

            Assert.Same(replacement, manager.GetExistingWindow(typeof(ManagedWindow)));
            replacement.Close();
        });
    }

    [Fact]
    public void WindowType_Null_ThrowsArgumentNullException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var manager = new DialogManager(provider);

        Assert.Equal(
            "windowType",
            Assert.Throws<ArgumentNullException>(() => manager.GetExistingWindow(null!)).ParamName);
        Assert.Equal(
            "windowType",
            Assert.Throws<ArgumentNullException>(() => manager.GetOrCreateWindow(null!)).ParamName);
    }

    [Fact]
    public void WindowType_NonWindow_ThrowsArgumentException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var manager = new DialogManager(provider);

        Assert.Equal(
            "windowType",
            Assert.Throws<ArgumentException>(() => manager.GetExistingWindow(typeof(string))).ParamName);
        Assert.Equal(
            "windowType",
            Assert.Throws<ArgumentException>(() => manager.GetOrCreateWindow(typeof(string))).ParamName);
    }

    [Fact]
    public void GetOrCreateWindow_MissingRegistration_PreservesGetRequiredServiceException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var manager = new DialogManager(provider);

        var exception = Assert.Throws<InvalidOperationException>(
            () => manager.GetOrCreateWindow(typeof(ManagedWindow)));

        Assert.Contains(typeof(ManagedWindow).ToString(), exception.Message);
    }

    [Fact]
    public void GetOrCreateWindow_ProviderReturnsNonWindow_ThrowsClearInvalidOperationException()
    {
        var manager = new DialogManager(new WrongTypeServiceProvider());

        var exception = Assert.Throws<InvalidOperationException>(
            () => manager.GetOrCreateWindow(typeof(ManagedWindow)));

        Assert.Contains(typeof(ManagedWindow).FullName!, exception.Message);
        Assert.Contains(typeof(string).FullName!, exception.Message);
    }

    [Fact]
    public void GetOrCreateWindow_SameTypeFactoryReentry_ThrowsAndCanRetry()
    {
        StaTest.Run(() =>
        {
            DialogManager? manager = null;
            var resolutionCount = 0;
            using var provider = new ServiceCollection()
                .AddTransient<ReentrantWindow>(_ =>
                {
                    resolutionCount++;
                    if (resolutionCount == 1)
                    {
                        return (ReentrantWindow)manager!.GetOrCreateWindow(typeof(ReentrantWindow));
                    }

                    return new ReentrantWindow();
                })
                .BuildServiceProvider();
            manager = new DialogManager(provider);

            var exception = Assert.Throws<InvalidOperationException>(
                () => manager.GetOrCreateWindow(typeof(ReentrantWindow)));

            Assert.Contains(typeof(ReentrantWindow).FullName!, exception.Message);
            Assert.Contains("already being resolved", exception.Message);
            Assert.Equal(1, resolutionCount);
            Assert.Null(manager.GetExistingWindow(typeof(ReentrantWindow)));

            var retry = manager.GetOrCreateWindow(typeof(ReentrantWindow));

            Assert.IsType<ReentrantWindow>(retry);
            Assert.Equal(2, resolutionCount);
            retry.Close();
        });
    }

    [Fact]
    public void GetOrCreateWindow_ConcurrentCallers_ResolveOneTransientInstance()
    {
        StaTest.Run(() =>
        {
            var provider = new CoordinatedServiceProvider();
            var manager = new DialogManager(provider);
            Window? firstResult = null;
            Window? secondResult = null;
            Exception? firstException = null;
            Exception? secondException = null;
            using var secondCallStarted = new ManualResetEventSlim();

            var firstThread = CreateStaThread(() =>
            {
                try
                {
                    firstResult = manager.GetOrCreateWindow(typeof(ConcurrentWindow));
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            });
            var secondThread = CreateStaThread(() =>
            {
                secondCallStarted.Set();
                try
                {
                    secondResult = manager.GetOrCreateWindow(typeof(ConcurrentWindow));
                }
                catch (Exception exception)
                {
                    secondException = exception;
                }
            });

            firstThread.Start();
            try
            {
                Assert.True(provider.FirstResolutionEntered.Wait(TimeSpan.FromSeconds(5)));
                secondThread.Start();
                Assert.True(secondCallStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(
                    SpinWait.SpinUntil(
                        () => Volatile.Read(ref provider.ResolutionCount) > 1
                            || (secondThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                        TimeSpan.FromSeconds(5)),
                    "The second caller neither entered DI resolution nor waited for the first caller.");
            }
            finally
            {
                provider.ReleaseFirstResolution.Set();
                Assert.True(firstThread.Join(TimeSpan.FromSeconds(5)));
                if (secondThread.ThreadState != ThreadState.Unstarted)
                {
                    Assert.True(secondThread.Join(TimeSpan.FromSeconds(5)));
                }
            }

            Assert.Null(firstException);
            Assert.Null(secondException);
            Assert.Equal(1, provider.ResolutionCount);
            Assert.NotNull(firstResult);
            Assert.Same(firstResult, secondResult);
            GC.KeepAlive(firstResult);
        });
    }

    [Fact]
    public void GetOrCreateWindow_RegistrationReturnsDifferentWindowType_ThrowsWithoutCaching()
    {
        StaTest.Run(() =>
        {
            using var provider = new ServiceCollection()
                .AddTransient(typeof(FirstWindow), _ => new SecondWindow())
                .BuildServiceProvider();
            var manager = new DialogManager(provider);

            var exception = Assert.Throws<InvalidOperationException>(
                () => manager.GetOrCreateWindow(typeof(FirstWindow)));

            Assert.Contains(typeof(FirstWindow).FullName!, exception.Message);
            Assert.Contains(typeof(SecondWindow).FullName!, exception.Message);
            Assert.Null(manager.GetExistingWindow(typeof(FirstWindow)));
        });
    }

    [Fact]
    public void ClosedWindow_RetainedByCaller_DoesNotKeepManagerAlive()
    {
        StaTest.Run(() =>
        {
            var retainedWindow = new ManagedWindow();
            var weakManager = CreateAndCloseManager(retainedWindow);

            ForceGarbageCollection(weakManager);

            Assert.False(weakManager.IsAlive);
            GC.KeepAlive(retainedWindow);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndCloseManager(ManagedWindow window)
    {
        var manager = new DialogManager(new FixedWindowServiceProvider(window));
        Assert.Same(window, manager.GetOrCreateWindow(typeof(ManagedWindow)));

        window.Show();
        StaTest.PumpDispatcher();
        window.Close();
        StaTest.PumpDispatcher();

        return new WeakReference(manager);
    }

    private static void ForceGarbageCollection(WeakReference weakReference)
    {
        const int attemptLimit = 10;

        for (var attempt = 0; attempt < attemptLimit && weakReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static Thread CreateStaThread(ThreadStart action)
    {
        var thread = new Thread(action)
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        return thread;
    }

    private sealed class ManagedWindow : Window
    {
    }

    private sealed class ReentrantWindow : Window
    {
    }

    private sealed class ConcurrentWindow : Window
    {
    }

    private sealed class WrongTypeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => "not a window";
    }

    private sealed class FixedWindowServiceProvider : IServiceProvider
    {
        private readonly Window window;

        public FixedWindowServiceProvider(Window window)
        {
            this.window = window;
        }

        public object? GetService(Type serviceType) => window;
    }

    private sealed class CoordinatedServiceProvider : IServiceProvider
    {
        public readonly ManualResetEventSlim FirstResolutionEntered = new();
        public readonly ManualResetEventSlim ReleaseFirstResolution = new();
        public int ResolutionCount;

        public object? GetService(Type serviceType)
        {
            var resolution = Interlocked.Increment(ref ResolutionCount);
            if (resolution == 1)
            {
                FirstResolutionEntered.Set();
                if (!ReleaseFirstResolution.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("The first resolution was not released by the test.");
                }
            }

            return new ConcurrentWindow();
        }
    }
}
