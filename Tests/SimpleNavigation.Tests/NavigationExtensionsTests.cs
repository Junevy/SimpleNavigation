using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface;
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
}
