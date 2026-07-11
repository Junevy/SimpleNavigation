using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface;
using SimpleNavigation.Services;
using SimpleNavigation.Tests.TestInfrastructure;
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
            services.AddPage<FirstPage>("page");
            services.AddContent<TestContent>("content");
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
        var regionManager = new RegionManager();
        services.AddSingleton<IRegionManager>(regionManager);
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPageService, PageService>();
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
    public void DoubleGenericOverloads_RegisterViewAndViewModelWithoutSettingDataContext()
    {
        StaTest.Run(() =>
        {
            var services = new ServiceCollection();
            services.AddPage<FirstPage, TestViewModel>();
            services.AddContent<TestContent, TestViewModel>();
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

            services.AddPage<FirstPage, TestViewModel>("page");
            services.AddContent<TestContent, TestViewModel>("content");
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
        services.AddPage<FirstPage>("main");
        services.AddPage<SecondPage>("Main");

        var exception = Assert.Throws<ArgumentException>(
            () => services.AddPage<SecondPage>("main"));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void DuplicateContentKey_IsRejected()
    {
        var services = new ServiceCollection();
        services.AddContent<TestContent>("main");

        var exception = Assert.Throws<ArgumentException>(
            () => services.AddContent<TestContent, TestViewModel>("main"));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void SameKeyAcrossPageAndContentCategories_IsAllowed()
    {
        var services = new ServiceCollection();

        services.AddPage<FirstPage>("main");
        services.AddContent<TestContent>("main");
    }

    [Fact]
    public void ContentRegistration_RejectsPageAndWindowTypes()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddContent<FirstPage>("page"));
        Assert.Throws<ArgumentException>(() => services.AddContent<Window>("window"));
        Assert.Throws<ArgumentException>(() => services.AddContent<FirstPage, TestViewModel>());
        Assert.Throws<ArgumentException>(() => services.AddContent<Window, TestViewModel>("window-vm"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidRouteKey_IsRejected(string? key)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ServiceCollection().AddPage<FirstPage>(key!));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidContentRouteKey_IsRejected(string? key)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ServiceCollection().AddContent<TestContent>(key!));

        Assert.Equal("key", exception.ParamName);
    }
}
