using System.Reflection;
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

    private sealed class ManagedWindow : Window
    {
    }

    private sealed class WrongTypeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => "not a window";
    }
}
