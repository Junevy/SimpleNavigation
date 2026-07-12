using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface.Managers;
using SimpleNavigation.Interface.Services;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SimpleNavigation.Tests;

public class PageServiceTests
{
    [Fact]
    public void GenericAndTypeNavigationDoNotRequireRoutes()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<FirstPage>();
            services.AddTransient<SecondPage>();
            using var provider = services.BuildServiceProvider();
            var frame = RegisterFrame(provider, "main");
            var service = provider.GetRequiredService<IPageService>();

            service.Navigate<FirstPage>("main");
            StaTest.PumpUntil(() => frame.Content is FirstPage);
            Assert.IsType<FirstPage>(frame.Content);

            service.Navigate("main", typeof(SecondPage));
            StaTest.PumpUntil(() => frame.Content is SecondPage);
            Assert.IsType<SecondPage>(frame.Content);
        });
    }

    [Fact]
    public void StringNavigationUsesCaseSensitiveRouteThenOrdinaryDi()
    {
        StaTest.Run(() =>
        {
            var expected = new FirstPage();
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(expected);
            services.AddPage<FirstPage>("first");
            using var provider = services.BuildServiceProvider();
            var frame = RegisterFrame(provider, "main");
            var service = provider.GetRequiredService<IPageService>();

            service.Navigate("main", "first");

            StaTest.PumpUntil(() => ReferenceEquals(frame.Content, expected));
            Assert.Same(expected, frame.Content);
            Assert.Throws<KeyNotFoundException>(() => service.Navigate("main", "First"));
        });
    }

    [Fact]
    public void ViewAndDataContextReceiveParametersOnceEach()
    {
        StaTest.Run(() =>
        {
            var viewModel = new AwareViewModel();
            var page = new AwarePage { DataContext = viewModel };
            var parameters = new DialogParameters("id", 7);
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(page);
            using var provider = services.BuildServiceProvider();
            var frame = RegisterFrame(provider, "main");

            provider.GetRequiredService<IPageService>()
                .Navigate<AwarePage>("main", parameters);

            Assert.Equal(1, page.CallCount);
            Assert.Equal(1, viewModel.CallCount);
            Assert.Same(parameters, page.Parameters);
            Assert.Same(parameters, viewModel.Parameters);
            GC.KeepAlive(frame);
        });
    }

    [Fact]
    public void NullParametersStillTriggerAwarenessAndSameInstanceIsDeduplicated()
    {
        StaTest.Run(() =>
        {
            var page = new AwarePage();
            page.DataContext = page;
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(page);
            using var provider = services.BuildServiceProvider();
            var frame = RegisterFrame(provider, "main");

            provider.GetRequiredService<IPageService>().Navigate<AwarePage>("main");

            Assert.Equal(1, page.CallCount);
            Assert.Null(page.Parameters);
            GC.KeepAlive(frame);
        });
    }

    [Fact]
    public void InvalidInputsFailFast()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<FirstPage>();
            using var provider = services.BuildServiceProvider();
            var contentHost = new ContentControl();
            provider.GetRequiredService<IRegionManager>().RegisterRegion("content", contentHost);
            var service = provider.GetRequiredService<IPageService>();

            Assert.Throws<ArgumentException>(() => service.Navigate<FirstPage>(" "));
            Assert.Throws<ArgumentNullException>(() => service.Navigate("content", (Type)null!));
            Assert.Throws<ArgumentException>(() => service.Navigate("content", typeof(TestContent)));
            Assert.Throws<InvalidOperationException>(() => service.Navigate<FirstPage>("missing"));
            Assert.Throws<InvalidOperationException>(() => service.Navigate<FirstPage>("content"));
            Assert.Throws<KeyNotFoundException>(() => service.Navigate("content", "unknown"));
            GC.KeepAlive(contentHost);
        });
    }

    [Fact]
    public void GoBackUsesTheFrameJournalAndLegacyMethodForwards()
    {
        StaTest.Run(() =>
        {
            var first = new FirstPage();
            var second = new SecondPage();
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(first);
            services.AddSingleton(second);
            using var provider = services.BuildServiceProvider();
            var frame = RegisterFrame(provider, "main");
            var service = provider.GetRequiredService<IPageService>();

            service.GoBack("main");
            service.Navigate<FirstPage>("main");
            StaTest.PumpUntil(() => ReferenceEquals(frame.Content, first));
            service.Navigate<SecondPage>("main");
            StaTest.PumpUntil(() => ReferenceEquals(frame.Content, second));
            Assert.True(frame.CanGoBack);

            service.GoBack("main");
            StaTest.PumpUntil(() => ReferenceEquals(frame.Content, first));
            Assert.Same(first, frame.Content);

            service.Navigate<SecondPage>("main");
            StaTest.PumpUntil(() => ReferenceEquals(frame.Content, second));

#pragma warning disable CS0618
            service.Goback("main");
#pragma warning restore CS0618
            StaTest.PumpUntil(() => ReferenceEquals(frame.Content, first));
            Assert.Same(first, frame.Content);
        });
    }

    [Fact]
    public void MissingDiRegistrationKeepsTheContainerException()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            using var provider = services.BuildServiceProvider();
            var frame = RegisterFrame(provider, "main");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IPageService>().Navigate<FirstPage>("main"));

            Assert.Contains(typeof(FirstPage).FullName!, exception.Message);
            GC.KeepAlive(frame);
        });
    }

    [Fact]
    public void AwarenessExceptionPropagatesToTheCaller()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(new ThrowingAwarePage());
            using var provider = services.BuildServiceProvider();
            var frame = RegisterFrame(provider, "main");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IPageService>().Navigate<ThrowingAwarePage>("main"));

            Assert.Equal("awareness failed", exception.Message);
            GC.KeepAlive(frame);
        });
    }

    [Fact]
    public void NavigationRequiresTheHostDispatcherThread()
    {
        ServiceProvider? provider = null;
        IPageService? service = null;
        Frame? frame = null;
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(new FirstPage());
            provider = services.BuildServiceProvider();
            frame = RegisterFrame(provider, "main");
            service = provider.GetRequiredService<IPageService>();
        });

        try
        {
            Assert.Throws<InvalidOperationException>(() => service!.Navigate<FirstPage>("main"));
            GC.KeepAlive(frame);
        }
        finally
        {
            provider!.Dispose();
        }
    }

    private static Frame RegisterFrame(IServiceProvider provider, string name)
    {
        var frame = new Frame
        {
            JournalOwnership = JournalOwnership.OwnsJournal,
            NavigationUIVisibility = NavigationUIVisibility.Hidden
        };
        provider.GetRequiredService<IRegionManager>().RegisterRegion(name, frame);
        return frame;
    }
}
