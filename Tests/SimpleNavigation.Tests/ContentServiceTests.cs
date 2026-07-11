using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Tests;

public class ContentServiceTests
{
    [Fact]
    public void ContentHostAdapterContractAcceptsAnyFrameworkElementHost()
    {
        var adapterType = typeof(IContentService).Assembly.GetType(
            "SimpleNavigation.Common.IContentRegionHostAdapter",
            throwOnError: true)!;
        var present = adapterType.GetMethod("Present");

        Assert.NotNull(present);
        Assert.Equal(
            typeof(FrameworkElement),
            present!.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void GenericAndTypeNavigationDoNotRequireRoutes()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<TestContent>();
            services.AddTransient<Grid>();
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(provider, "main");
            var service = provider.GetRequiredService<IContentService>();

            service.Navigate<TestContent>("main");
            Assert.IsType<TestContent>(host.Content);

            service.Navigate("main", typeof(Grid));
            Assert.IsType<Grid>(host.Content);
        });
    }

    [Fact]
    public void StringNavigationUsesCaseSensitiveRouteThenOrdinaryDi()
    {
        StaTest.Run(() =>
        {
            var expected = new TestContent();
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(expected);
            services.AddContent<TestContent>("content");
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(provider, "main");
            var service = provider.GetRequiredService<IContentService>();

            service.Navigate("main", "content");

            Assert.Same(expected, host.Content);
            Assert.Throws<KeyNotFoundException>(() => service.Navigate("main", "Content"));
        });
    }

    [Fact]
    public void ViewThenDistinctDataContextReceiveTheSameParameters()
    {
        StaTest.Run(() =>
        {
            var calls = new List<string>();
            var viewModel = new RecordingAware("context", calls);
            var content = new RecordingAwareContent("view", calls)
            {
                DataContext = viewModel,
            };
            var parameters = new DialogParameters("id", 9);
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(content);
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(provider, "main");

            provider.GetRequiredService<IContentService>()
                .Navigate<RecordingAwareContent>("main", parameters);

            Assert.Equal(new[] { "view", "context" }, calls);
            Assert.Same(parameters, content.Parameters);
            Assert.Same(parameters, viewModel.Parameters);
            Assert.Same(content, host.Content);
        });
    }

    [Fact]
    public void NullParametersStillNotifyAndSameInstanceIsDeduplicated()
    {
        StaTest.Run(() =>
        {
            var content = new AwareContent();
            content.DataContext = content;
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(content);
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(provider, "main");

            provider.GetRequiredService<IContentService>()
                .Navigate<AwareContent>("main");

            Assert.Equal(1, content.CallCount);
            Assert.Null(content.Parameters);
            Assert.Same(content, host.Content);
        });
    }

    [Fact]
    public void InvalidInputsAndUnsupportedTargetsFailFast()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<FirstPage>();
            services.AddTransient<Window>();
            services.AddTransient<TestContent>();
            using var provider = services.BuildServiceProvider();
            var frame = new Frame();
            provider.GetRequiredService<IRegionManager>().RegisterRegion("frame", frame);
            var service = provider.GetRequiredService<IContentService>();

            Assert.Throws<ArgumentException>(() => service.Navigate<TestContent>(" "));
            Assert.Throws<ArgumentNullException>(() => service.Navigate("frame", (Type)null!));
            Assert.Throws<ArgumentException>(() => service.Navigate("frame", typeof(string)));
            Assert.Throws<ArgumentException>(() => service.Navigate<FirstPage>("frame"));
            Assert.Throws<ArgumentException>(() => service.Navigate("frame", typeof(Window)));
            Assert.Throws<InvalidOperationException>(() => service.Navigate<TestContent>("frame"));
            GC.KeepAlive(frame);
        });
    }

    [Fact]
    public void MissingRegionUnknownKeyAndMissingDiFailFast()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransient<TestContent>();
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(provider, "main");
            var service = provider.GetRequiredService<IContentService>();

            Assert.Throws<InvalidOperationException>(
                () => service.Navigate<TestContent>("missing"));
            Assert.Throws<KeyNotFoundException>(
                () => service.Navigate("main", "unknown"));
            var exception = Assert.Throws<InvalidOperationException>(
                () => service.Navigate("main", typeof(Grid)));
            Assert.Contains(typeof(Grid).FullName!, exception.Message);
            GC.KeepAlive(host);
        });
    }

    [Fact]
    public void DoubleGenericRegistrationWithoutKeyDoesNotCreateARoute()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddContent<TestContent, TestViewModel>();
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(provider, "main");

            Assert.Throws<KeyNotFoundException>(() =>
                provider.GetRequiredService<IContentService>()
                    .Navigate("main", "content"));
            GC.KeepAlive(host);
        });
    }

    [Fact]
    public void AwarenessExceptionPropagatesAfterPresentation()
    {
        StaTest.Run(() =>
        {
            var content = new ThrowingAwareContent();
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(content);
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(provider, "main");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IContentService>()
                    .Navigate<ThrowingAwareContent>("main"));

            Assert.Equal("awareness failed", exception.Message);
            Assert.Same(content, host.Content);
        });
    }

    [Fact]
    public void HostPresentationExceptionPropagatesWithoutNotifyingAwareness()
    {
        StaTest.Run(() =>
        {
            var content = new AwareContent();
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(content);
            using var provider = services.BuildServiceProvider();
            var host = RegisterContentHost(
                provider,
                "main",
                new ThrowingContentControl());
            var regionManager = provider.GetRequiredService<IRegionManager>();

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    provider.GetRequiredService<IContentService>()
                        .Navigate<AwareContent>("main"));

                Assert.Equal("presentation failed", exception.Message);
                Assert.Equal(0, content.CallCount);
            }
            finally
            {
                regionManager.UnregisterRegion("main", host);
            }
        });
    }

    [Fact]
    public void NavigationRequiresTheHostDispatcherThread()
    {
        ServiceProvider? provider = null;
        IContentService? service = null;
        ContentControl? host = null;
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddSingleton(new TestContent());
            provider = services.BuildServiceProvider();
            host = RegisterContentHost(provider, "main");
            service = provider.GetRequiredService<IContentService>();
        });

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                service!.Navigate<TestContent>("main"));
            GC.KeepAlive(host);
        }
        finally
        {
            provider!.Dispose();
        }
    }

    private static ContentControl RegisterContentHost(
        IServiceProvider provider,
        string name)
    {
        return RegisterContentHost(provider, name, new ContentControl());
    }

    private static TContentControl RegisterContentHost<TContentControl>(
        IServiceProvider provider,
        string name,
        TContentControl host)
        where TContentControl : ContentControl
    {
        provider.GetRequiredService<IRegionManager>().RegisterRegion(name, host);
        return host;
    }

    private sealed class ThrowingContentControl : ContentControl
    {
        protected override void OnContentChanged(
            object oldContent,
            object newContent)
        {
            throw new InvalidOperationException("presentation failed");
        }
    }

    private sealed class RecordingAwareContent : UserControl, INavigationAware
    {
        private readonly string name;
        private readonly IList<string> calls;

        public RecordingAwareContent(string name, IList<string> calls)
        {
            this.name = name;
            this.calls = calls;
        }

        public DialogParameters? Parameters { get; private set; }

        public void OnNavigated(DialogParameters? parameters)
        {
            calls.Add(name);
            Parameters = parameters;
        }
    }

    private sealed class RecordingAware : INavigationAware
    {
        private readonly string name;
        private readonly IList<string> calls;

        public RecordingAware(string name, IList<string> calls)
        {
            this.name = name;
            this.calls = calls;
        }

        public DialogParameters? Parameters { get; private set; }

        public void OnNavigated(DialogParameters? parameters)
        {
            calls.Add(name);
            Parameters = parameters;
        }
    }
}
