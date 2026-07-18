using System.Threading;
using System.Windows.Threading;
using SimpleNavigation.Common;
using SimpleNavigation.Tests.TestInfrastructure;

namespace SimpleNavigation.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void DialogParameters_RoundTripsValue()
    {
        var parameters = new DialogParameters("answer", 42);

        Assert.Equal(42, parameters.Get<int>("answer"));
    }

    [Fact]
    public void StaTest_Run_ExecutesOnStaThread()
    {
        ApartmentState? apartmentState = null;

        StaTest.Run(() => apartmentState = Thread.CurrentThread.GetApartmentState());

        Assert.Equal(ApartmentState.STA, apartmentState);
    }

    [Fact]
    public void StaTest_PumpUntil_ProcessesQueuedDispatcherWork()
    {
        var workProcessed = false;

        StaTest.Run(() =>
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() => workProcessed = true));

            StaTest.PumpUntil(() => workProcessed);
        });

        Assert.True(workProcessed);
    }

    [Fact]
    public void StaTest_PumpUntil_TimesOutWhenIdleWorkIsStarved()
    {
        var exception = Assert.Throws<TimeoutException>(() => StaTest.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherOperationCallback? keepDispatcherBusy = null;

            keepDispatcherBusy = _ =>
            {
                dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    keepDispatcherBusy!,
                    null);
                return null;
            };

            dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                keepDispatcherBusy,
                null);

            StaTest.PumpUntil(() => false);
        }));

        Assert.StartsWith("The dispatcher condition was not met", exception.Message);
    }
}
