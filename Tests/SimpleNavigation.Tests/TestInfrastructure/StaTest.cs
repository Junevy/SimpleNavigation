using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;

namespace SimpleNavigation.Tests.TestInfrastructure;

internal static class StaTest
{
    private static readonly TimeSpan ThreadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PumpSlice = TimeSpan.FromMilliseconds(50);

    public static void Run(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        ExceptionDispatchInfo? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                capturedException = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                try
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
                catch (Exception exception)
                {
                    capturedException ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(ThreadTimeout))
        {
            throw new TimeoutException($"The STA test thread did not finish within {ThreadTimeout.TotalSeconds} seconds.");
        }

        capturedException?.Throw();
    }

    public static void PumpDispatcher()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();

        var idleOperation = dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }),
            null);

        var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher)
        {
            Interval = PumpSlice,
        };
        EventHandler stopFrame = (_, _) => frame.Continue = false;
        timer.Tick += stopFrame;
        timer.Start();

        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
            timer.Tick -= stopFrame;

            if (idleOperation.Status == DispatcherOperationStatus.Pending)
            {
                idleOperation.Abort();
            }
        }
    }

    public static void PumpUntil(Func<bool> condition)
    {
        if (condition is null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed >= PumpTimeout)
            {
                throw new TimeoutException($"The dispatcher condition was not met within {PumpTimeout.TotalSeconds} seconds.");
            }

            PumpDispatcher();
        }
    }
}
