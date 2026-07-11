using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Windows.Controls;

namespace SimpleNavigation.Services;

public class PageService : IPageService
{
    private readonly IServiceProvider provider;
    private readonly IRegionManager regionManager;
    private readonly NavigationRouteRegistry routes;

    public PageService(IServiceProvider provider, IRegionManager regionManager)
    {
        this.provider = provider;
        this.regionManager = regionManager;
        routes = provider.GetRequiredService<NavigationRouteRegistry>();
    }

    public void Navigate<TPage>(
        string regionName,
        DialogParameters? parameters = null)
        where TPage : Page
    {
        ValidateRegionName(regionName);
        NavigateCore(
            regionName,
            provider.GetRequiredService<TPage>(),
            parameters);
    }

    public void Navigate(
        string regionName,
        Type targetType,
        DialogParameters? parameters = null)
    {
        ValidateRegionName(regionName);
        ValidatePageType(targetType);
        var page = provider.GetRequiredService(targetType) as Page
            ?? throw new InvalidOperationException(
                $"Resolved service '{targetType.FullName}' is not a Page.");
        NavigateCore(regionName, page, parameters);
    }

    public void Navigate(
        string regionName,
        string key,
        DialogParameters? parameters = null)
    {
        ValidateRegionName(regionName);
        var targetType = routes.GetRequiredPageType(key);
        var page = provider.GetRequiredService(targetType) as Page
            ?? throw new InvalidOperationException(
                $"Resolved service '{targetType.FullName}' is not a Page.");
        NavigateCore(regionName, page, parameters);
    }

    public void GoBack(string regionName)
    {
        var frame = GetRequiredFrame(regionName);
        var adapter = (IPageRegionHostAdapter)
            RegionHostAdapterResolver.GetRequired(frame);
        if (adapter.CanGoBack(frame))
        {
            adapter.GoBack(frame);
        }
    }

    [Obsolete("Use GoBack instead.")]
    public void Goback(string regionName)
    {
        GoBack(regionName);
    }

    private void NavigateCore(
        string regionName,
        Page page,
        DialogParameters? parameters)
    {
        var frame = GetRequiredFrame(regionName);
        var adapter = (IPageRegionHostAdapter)
            RegionHostAdapterResolver.GetRequired(frame);
        if (adapter.Navigate(frame, page))
        {
            NavigationAwareNotifier.Notify(page, parameters);
        }
    }

    private Frame GetRequiredFrame(string regionName)
    {
        ValidateRegionName(regionName);
        var region = regionManager.GetRegion(regionName);
        if (region is Frame frame)
        {
            return frame;
        }

        var actual = region?.GetType().FullName ?? "missing";
        throw new InvalidOperationException(
            $"Region '{regionName}' must be a Frame but was '{actual}'.");
    }

    private static void ValidatePageType(Type targetType)
    {
        if (targetType == null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        if (!typeof(Page).IsAssignableFrom(targetType))
        {
            throw new ArgumentException(
                $"Target type '{targetType.FullName}' must derive from Page.",
                nameof(targetType));
        }
    }

    private static void ValidateRegionName(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName))
        {
            throw new ArgumentException(
                "Region name cannot be null or whitespace.",
                nameof(regionName));
        }
    }
}
