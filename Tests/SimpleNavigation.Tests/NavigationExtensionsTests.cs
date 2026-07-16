using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common.Managers;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface.Managers;
using SimpleNavigation.Interface.Services;
using SimpleNavigation.Services;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;

namespace SimpleNavigation.Tests;

public sealed class NavigationExtensionsTests
{
    [Fact]
    public void SingleGenericKeyOverloads_RegisterTransientViewsAndCoreServices()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddTransientPage<FirstPage>("page");
            services.AddTransientContent<TestContent>("content");
            using var provider = services.BuildServiceProvider();

            Assert.NotSame(
                provider.GetRequiredService<FirstPage>(),
                provider.GetRequiredService<FirstPage>());
            Assert.NotSame(
                provider.GetRequiredService<TestContent>(),
                provider.GetRequiredService<TestContent>());
            Assert.Same(
                provider.GetRequiredService<IRegionManager>(),
                provider.GetRequiredService<IRegionManager>());
        });
    }

    [Fact]
    public void RegisterNavigationService_RegistersConcreteRegionManagerAsSingleton()
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRegionManager>();
        var second = provider.GetRequiredService<IRegionManager>();

        Assert.IsType<RegionManager>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void RegisterNavigationService_RegistersInternalRouteRegistryAsSingleton()
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        var descriptor = Assert.Single(
            services,
            item => item.ServiceType.FullName ==
                "SimpleNavigation.Common.NavigationRouteRegistry");
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService(descriptor.ServiceType);
        var second = provider.GetRequiredService(descriptor.ServiceType);

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(first, second);
    }

    [Fact]
    public void RegisterNavigationService_PreservesEarlierCoreRegistrations()
    {
        var services = new ServiceCollection();
        using var regionManager = new RegionManager();
        services.AddSingleton<IRegionManager>(regionManager);
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPageService, PageService>();
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IDialogManager, DialogManager>();
        var existingDescriptors = services.ToArray();

        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(existingDescriptors.Length + 1, services.Count);
        foreach (var descriptor in existingDescriptors)
        {
            Assert.Same(
                descriptor,
                Assert.Single(services, item => item.ServiceType == descriptor.ServiceType));
        }

        Assert.Same(regionManager, provider.GetRequiredService<IRegionManager>());
    }

    [Fact]
    public void RouteRegistry_MapsRoutesAddedBeforeAndAfterCoreRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingletonPage<FirstPage>("shared");
        services.RegisterNavigationService();
        services.AddSingletonContent<TestContent>("shared");
        services.AddSingletonPage<SecondPage>("second");
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Equal(
            typeof(FirstPage),
            InvokeRouteLookup(registry, "GetRequiredPageType", "shared"));
        Assert.Equal(
            typeof(TestContent),
            InvokeRouteLookup(registry, "GetRequiredContentType", "shared"));
        Assert.Equal(
            typeof(SecondPage),
            InvokeRouteLookup(registry, "GetRequiredPageType", "second"));
    }

    [Fact]
    public void RouteRegistry_UsesOrdinalCaseSensitiveKeys()
    {
        var services = new ServiceCollection();
        services.AddSingletonPage<FirstPage>("main");
        services.AddSingletonPage<SecondPage>("Main");
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Equal(
            typeof(FirstPage),
            InvokeRouteLookup(registry, "GetRequiredPageType", "main"));
        Assert.Equal(
            typeof(SecondPage),
            InvokeRouteLookup(registry, "GetRequiredPageType", "Main"));
    }

    [Fact]
    public void RouteRegistry_UnknownPageAndContentKeysThrowKeyNotFoundException()
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Throws<KeyNotFoundException>(
            () => InvokeRouteLookup(registry, "GetRequiredPageType", "missing"));
        Assert.Throws<KeyNotFoundException>(
            () => InvokeRouteLookup(registry, "GetRequiredContentType", "missing"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RouteRegistry_InvalidPageAndContentKeysThrowArgumentException(string? key)
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Throws<ArgumentException>(
            () => InvokeRouteLookup(registry, "GetRequiredPageType", key));
        Assert.Throws<ArgumentException>(
            () => InvokeRouteLookup(registry, "GetRequiredContentType", key));
    }

    [Fact]
    public void DoubleGenericOverloads_RegisterViewAndViewModelWithoutSettingDataContext()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.AddTransientPage<FirstPage, TestViewModel>();
            services.AddTransientContent<TestContent, TestViewModel>();
            using var provider = services.BuildServiceProvider();

            var page = provider.GetRequiredService<FirstPage>();
            var content = provider.GetRequiredService<TestContent>();

            Assert.Null(page.DataContext);
            Assert.Null(content.DataContext);
            Assert.NotSame(
                provider.GetRequiredService<TestViewModel>(),
                provider.GetRequiredService<TestViewModel>());
        });
    }

    [Fact]
    public void RegistrationHelpers_PreserveEarlierSingletonRegistrations()
    {
        StaTest.Run(() =>
        {
            var page = new FirstPage();
            var content = new TestContent();
            var viewModel = new TestViewModel();
            var services = new ServiceCollection();
            services.AddSingleton(page);
            services.AddSingleton(content);
            services.AddSingleton(viewModel);

            services.AddSingletonPage<FirstPage, TestViewModel>("page");
            services.AddSingletonContent<TestContent, TestViewModel>("content");
            using var provider = services.BuildServiceProvider();

            Assert.Same(page, provider.GetRequiredService<FirstPage>());
            Assert.Same(content, provider.GetRequiredService<TestContent>());
            Assert.Same(viewModel, provider.GetRequiredService<TestViewModel>());
        });
    }

    [Fact]
    public void DuplicateKeyWithinOneCategory_IsRejectedUsingOrdinalMatching()
    {
        var services = new ServiceCollection();
        services.AddSingletonPage<FirstPage>("main");
        services.AddSingletonPage<SecondPage>("Main");

        var exception = Assert.Throws<ArgumentException>(
            () => services.AddSingletonPage<SecondPage>("main"));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void DuplicateContentKey_IsRejected()
    {
        var services = new ServiceCollection();
        services.AddSingletonContent<TestContent>("main");

        var exception = Assert.Throws<ArgumentException>(
            () => services.AddSingletonContent<TestContent, TestViewModel>("main"));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void SameKeyAcrossPageAndContentCategories_IsAllowed()
    {
        var services = new ServiceCollection();

        services.AddSingletonPage<FirstPage>("main");
        services.AddSingletonContent<TestContent>("main");
    }

    [Fact]
    public void ContentRegistration_RejectsPageAndWindowTypes()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddSingletonContent<FirstPage>("page"));
        Assert.Throws<ArgumentException>(() => services.AddSingletonContent<Window>("window"));
        Assert.Throws<ArgumentException>(() => services.AddSingletonContent<FirstPage, TestViewModel>());
        Assert.Throws<ArgumentException>(() => services.AddSingletonContent<Window, TestViewModel>("window-vm"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidRouteKey_IsRejected(string? key)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ServiceCollection().AddSingletonPage<FirstPage>(key!));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidContentRouteKey_IsRejected(string? key)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ServiceCollection().AddSingletonContent<TestContent>(key!));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void DialogRegistration_AddWindowWithKey_RegistersTransientWindowAndRoute()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.AddWindow<FirstWindow>("first");
            services.RegisterNavigationService();
            using var provider = services.BuildServiceProvider();
            var registry = GetRouteRegistry(services, provider);

            Assert.NotSame(
                provider.GetRequiredService<FirstWindow>(),
                provider.GetRequiredService<FirstWindow>());
            Assert.Equal(
                typeof(FirstWindow),
                InvokeRouteLookup(registry, "GetRequiredDialogType", "first"));
        });
    }

    [Fact]
    public void DialogRegistration_AddWindowWithViewModel_RegistersBothTransientWithoutSettingDataContext()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.AddWindow<FirstWindow, DialogViewModel>();
            using var provider = services.BuildServiceProvider();

            var firstWindow = provider.GetRequiredService<FirstWindow>();
            var secondWindow = provider.GetRequiredService<FirstWindow>();

            Assert.NotSame(firstWindow, secondWindow);
            Assert.Null(firstWindow.DataContext);
            Assert.NotSame(
                provider.GetRequiredService<DialogViewModel>(),
                provider.GetRequiredService<DialogViewModel>());
        });
    }

    [Fact]
    public void DialogRegistration_AddWindowWithViewModelAndKey_RegistersBothTransientAndRoute()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.RegisterNavigationService();
            services.AddWindow<FirstWindow, DialogViewModel>("first");
            using var provider = services.BuildServiceProvider();
            var registry = GetRouteRegistry(services, provider);

            Assert.NotSame(
                provider.GetRequiredService<FirstWindow>(),
                provider.GetRequiredService<FirstWindow>());
            Assert.NotSame(
                provider.GetRequiredService<DialogViewModel>(),
                provider.GetRequiredService<DialogViewModel>());
            Assert.Equal(
                typeof(FirstWindow),
                InvokeRouteLookup(registry, "GetRequiredDialogType", "first"));
        });
    }

    [Fact]
    public void DialogRegistration_AddWindowPreservesEarlierSingletonRegistrations()
    {
        StaTest.Run(() =>
        {
            var window = new FirstWindow();
            var viewModel = new DialogViewModel();
            var services = new ServiceCollection();
            services.AddSingleton(window);
            services.AddSingleton(viewModel);

            services.AddWindow<FirstWindow, DialogViewModel>("first");
            using var provider = services.BuildServiceProvider();

            Assert.Same(window, provider.GetRequiredService<FirstWindow>());
            Assert.Same(viewModel, provider.GetRequiredService<DialogViewModel>());
        });
    }

    [Fact]
    public void DialogRoutes_UseOrdinalCaseSensitiveKeysAndRejectDuplicatesWithinDialogOnly()
    {
        var services = new ServiceCollection();
        services.AddSingletonPage<FirstPage>("main");
        services.AddSingletonContent<TestContent>("main");
        services.AddWindow<FirstWindow>("main");
        services.AddWindow<SecondWindow>("Main");
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        var exception = Assert.Throws<ArgumentException>(
            () => services.AddWindow<SecondWindow>("main"));

        Assert.Equal("key", exception.ParamName);
        Assert.Equal(
            typeof(FirstWindow),
            InvokeRouteLookup(registry, "GetRequiredDialogType", "main"));
        Assert.Equal(
            typeof(SecondWindow),
            InvokeRouteLookup(registry, "GetRequiredDialogType", "Main"));
    }

    [Fact]
    public void DialogRoutes_RegisteredBeforeAndAfterCoreRegistration_AreAvailable()
    {
        var services = new ServiceCollection();
        services.AddWindow<FirstWindow>("first");
        services.RegisterNavigationService();
        services.AddWindow<SecondWindow>("second");
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Equal(
            typeof(FirstWindow),
            InvokeRouteLookup(registry, "GetRequiredDialogType", "first"));
        Assert.Equal(
            typeof(SecondWindow),
            InvokeRouteLookup(registry, "GetRequiredDialogType", "second"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DialogRoutes_InvalidKeysThrowArgumentException(string? key)
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Throws<ArgumentException>(
            () => InvokeRouteLookup(registry, "GetRequiredDialogType", key));
    }

    [Fact]
    public void DialogRoutes_UnknownKeyThrowsKeyNotFoundException()
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Throws<KeyNotFoundException>(
            () => InvokeRouteLookup(registry, "GetRequiredDialogType", "missing"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DialogRegistration_InvalidKeyDoesNotModifyServiceCollection(string? key)
    {
        var services = new ServiceCollection();
        var originalDescriptors = services.ToArray();

        var windowException = Assert.Throws<ArgumentException>(
            () => services.AddWindow<FirstWindow>(key!));

        Assert.Equal("key", windowException.ParamName);
        Assert.Equal(originalDescriptors.Length, services.Count);
        Assert.True(originalDescriptors.SequenceEqual(services));

        var viewModelException = Assert.Throws<ArgumentException>(
            () => services.AddWindow<FirstWindow, DialogViewModel>(key!));

        Assert.Equal("key", viewModelException.ParamName);
        Assert.Equal(originalDescriptors.Length, services.Count);
        Assert.True(originalDescriptors.SequenceEqual(services));
    }

    [Fact]
    public void DialogRegistration_DuplicateKeyDoesNotAddDescriptorsOrReplaceRoute()
    {
        var services = new ServiceCollection();
        services.AddWindow<FirstWindow>("main");
        var originalDescriptors = services.ToArray();

        var exception = Assert.Throws<ArgumentException>(
            () => services.AddWindow<SecondWindow, DialogViewModel>("main"));

        Assert.Equal("key", exception.ParamName);
        Assert.Equal(originalDescriptors.Length, services.Count);
        Assert.True(originalDescriptors.SequenceEqual(services));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(SecondWindow));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(DialogViewModel));

        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var registry = GetRouteRegistry(services, provider);

        Assert.Equal(
            typeof(FirstWindow),
            InvokeRouteLookup(registry, "GetRequiredDialogType", "main"));
    }

    private static object GetRouteRegistry(
        IServiceCollection services,
        IServiceProvider provider)
    {
        var descriptor = Assert.Single(
            services,
            item => item.ServiceType.FullName ==
                "SimpleNavigation.Common.NavigationRouteRegistry");
        return provider.GetRequiredService(descriptor.ServiceType);
    }

    private static Type InvokeRouteLookup(
        object registry,
        string methodName,
        string? key)
    {
        var method = registry.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        try
        {
            return Assert.IsAssignableFrom<Type>(
                method!.Invoke(registry, new object?[] { key }));
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
