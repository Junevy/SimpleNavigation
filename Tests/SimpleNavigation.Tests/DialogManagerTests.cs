using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
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
    public void GetOrCreateWindow_BlockedType_DoesNotBlockUnrelatedOperationsAndResolvesOnce()
    {
        StaTest.Run(() =>
        {
            using var provider = new MultiTypeCoordinatedServiceProvider();
            var manager = new DialogManager(provider);
            Window? ownerResult = null;
            Window? waiterResult = null;
            Window? existingResult = null;
            Window? unrelatedResult = null;
            Exception? ownerException = null;
            Exception? waiterException = null;
            Exception? existingException = null;
            Exception? unrelatedException = null;
            using var waiterStarted = new ManualResetEventSlim();
            using var existingCompleted = new ManualResetEventSlim();
            using var unrelatedCompleted = new ManualResetEventSlim();
            using var ownerMayClose = new ManualResetEventSlim();

            var ownerThread = CreateStaThread(() =>
            {
                try
                {
                    ownerResult = manager.GetOrCreateWindow(typeof(FirstWindow));
                    ownerMayClose.Wait(TimeSpan.FromSeconds(10));
                    ownerResult.Close();
                }
                catch (Exception exception)
                {
                    ownerException = exception;
                }
            });
            var waiterThread = CreateStaThread(() =>
            {
                waiterStarted.Set();
                try
                {
                    waiterResult = manager.GetOrCreateWindow(typeof(FirstWindow));
                }
                catch (Exception exception)
                {
                    waiterException = exception;
                }
            });
            var existingThread = CreateStaThread(() =>
            {
                try
                {
                    existingResult = manager.GetExistingWindow(typeof(FirstWindow));
                }
                catch (Exception exception)
                {
                    existingException = exception;
                }
                finally
                {
                    existingCompleted.Set();
                }
            });
            var unrelatedThread = CreateStaThread(() =>
            {
                try
                {
                    unrelatedResult = manager.GetOrCreateWindow(typeof(SecondWindow));
                    unrelatedResult.Close();
                }
                catch (Exception exception)
                {
                    unrelatedException = exception;
                }
                finally
                {
                    unrelatedCompleted.Set();
                }
            });

            var existingMadeProgress = false;
            var unrelatedMadeProgress = false;
            ownerThread.Start();
            try
            {
                Assert.True(provider.FirstResolutionEntered.Wait(TimeSpan.FromSeconds(5)));
                waiterThread.Start();
                Assert.True(waiterStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(
                    SpinWait.SpinUntil(
                        () => (waiterThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                        TimeSpan.FromSeconds(5)),
                    "The same-type caller did not wait for the in-flight resolution.");

                existingThread.Start();
                unrelatedThread.Start();
                existingMadeProgress = existingCompleted.Wait(TimeSpan.FromSeconds(2));
                unrelatedMadeProgress = unrelatedCompleted.Wait(TimeSpan.FromSeconds(2));
            }
            finally
            {
                provider.ReleaseFirstResolution.Set();
                JoinIfStarted(waiterThread);
                JoinIfStarted(existingThread);
                JoinIfStarted(unrelatedThread);
                ownerMayClose.Set();
                JoinIfStarted(ownerThread);
            }

            Assert.True(existingMadeProgress, "GetExistingWindow blocked behind unrelated DI resolution.");
            Assert.True(unrelatedMadeProgress, "A different window type blocked behind unrelated DI resolution.");
            Assert.Null(ownerException);
            Assert.Null(waiterException);
            Assert.Null(existingException);
            Assert.Null(unrelatedException);
            Assert.Null(existingResult);
            Assert.Equal(1, provider.FirstResolutionCount);
            Assert.NotNull(ownerResult);
            Assert.Same(ownerResult, waiterResult);
            Assert.IsType<SecondWindow>(unrelatedResult);
            GC.KeepAlive(ownerResult);
        });
    }

    [Fact]
    public void GetOrCreateWindow_FailedResolution_WakesWaitersAndLaterCallRetries()
    {
        StaTest.Run(() =>
        {
            using var provider = new FailingThenSuccessfulServiceProvider();
            var manager = new DialogManager(provider);
            Exception? ownerException = null;
            Exception? waiterException = null;
            Window? waiterResult = null;
            using var waiterStarted = new ManualResetEventSlim();

            var ownerThread = CreateStaThread(() =>
            {
                try
                {
                    manager.GetOrCreateWindow(typeof(FailureWindow));
                }
                catch (Exception exception)
                {
                    ownerException = exception;
                }
            });
            var waiterThread = CreateStaThread(() =>
            {
                waiterStarted.Set();
                try
                {
                    waiterResult = manager.GetOrCreateWindow(typeof(FailureWindow));
                }
                catch (Exception exception)
                {
                    waiterException = exception;
                }
                finally
                {
                    if (waiterResult != null)
                    {
                        waiterResult.Close();
                    }
                }
            });

            ownerThread.Start();
            try
            {
                Assert.True(provider.FirstResolutionEntered.Wait(TimeSpan.FromSeconds(5)));
                waiterThread.Start();
                Assert.True(waiterStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(
                    SpinWait.SpinUntil(
                        () => (waiterThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                        TimeSpan.FromSeconds(5)),
                    "The waiter did not wait for the failing in-flight resolution.");
            }
            finally
            {
                provider.ReleaseFirstResolution.Set();
                JoinIfStarted(ownerThread);
                JoinIfStarted(waiterThread);
            }

            Assert.Same(provider.ExpectedFailure, ownerException);
            Assert.Same(provider.ExpectedFailure, waiterException);
            Assert.Null(waiterResult);
            Assert.Equal(1, provider.ResolutionCount);

            var retry = manager.GetOrCreateWindow(typeof(FailureWindow));

            Assert.IsType<FailureWindow>(retry);
            Assert.Equal(2, provider.ResolutionCount);
            retry.Close();
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
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        return thread;
    }

    private static void JoinIfStarted(Thread thread)
    {
        if (thread.ThreadState != ThreadState.Unstarted)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "An STA worker did not finish.");
        }
    }

    private sealed class ManagedWindow : Window
    {
    }

    private sealed class ReentrantWindow : Window
    {
    }

    private sealed class FailureWindow : Window
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

    private sealed class MultiTypeCoordinatedServiceProvider : IServiceProvider, IDisposable
    {
        public readonly ManualResetEventSlim FirstResolutionEntered = new();
        public readonly ManualResetEventSlim ReleaseFirstResolution = new();
        public int FirstResolutionCount;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(FirstWindow))
            {
                Interlocked.Increment(ref FirstResolutionCount);
                FirstResolutionEntered.Set();
                if (!ReleaseFirstResolution.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new TimeoutException("The first resolution was not released by the test.");
                }

                return new FirstWindow();
            }

            if (serviceType == typeof(SecondWindow))
            {
                return new SecondWindow();
            }

            return null;
        }

        public void Dispose()
        {
            FirstResolutionEntered.Dispose();
            ReleaseFirstResolution.Dispose();
        }
    }

    private sealed class FailingThenSuccessfulServiceProvider : IServiceProvider, IDisposable
    {
        public readonly ManualResetEventSlim FirstResolutionEntered = new();
        public readonly ManualResetEventSlim ReleaseFirstResolution = new();
        public readonly InvalidOperationException ExpectedFailure = new("expected resolution failure");
        public int ResolutionCount;

        public object? GetService(Type serviceType)
        {
            var resolution = Interlocked.Increment(ref ResolutionCount);
            if (resolution == 1)
            {
                FirstResolutionEntered.Set();
                if (!ReleaseFirstResolution.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new TimeoutException("The failing resolution was not released by the test.");
                }

                throw ExpectedFailure;
            }

            return new FailureWindow();
        }

        public void Dispose()
        {
            FirstResolutionEntered.Dispose();
            ReleaseFirstResolution.Dispose();
        }
    }
}
