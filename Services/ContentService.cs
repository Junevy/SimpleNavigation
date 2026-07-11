using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Services;

public class ContentService : IContentService
{
    private readonly IServiceProvider provider;
    private readonly IRegionManager regionManager;
    private readonly NavigationRouteRegistry routes;

    public ContentService(IServiceProvider provider, IRegionManager regionManager)
    {
        this.provider = provider;
        this.regionManager = regionManager;
        routes = provider.GetRequiredService<NavigationRouteRegistry>();
    }

    public void Navigate<TContent>(
        string regionName,
        DialogParameters? parameters = null)
        where TContent : FrameworkElement
    {
        ValidateRegionName(regionName);
        ValidateContentType(typeof(TContent));
        NavigateCore(
            regionName,
            provider.GetRequiredService<TContent>(),
            parameters);
    }

    public void Navigate(
        string regionName,
        Type targetType,
        DialogParameters? parameters = null)
    {
        ValidateRegionName(regionName);
        ValidateContentType(targetType);
        var content = provider.GetRequiredService(targetType) as FrameworkElement
            ?? throw new InvalidOperationException(
                $"Resolved service '{targetType.FullName}' is not a FrameworkElement.");
        NavigateCore(regionName, content, parameters);
    }

    public void Navigate(
        string regionName,
        string key,
        DialogParameters? parameters = null)
    {
        ValidateRegionName(regionName);
        var targetType = routes.GetRequiredContentType(key);
        ValidateContentType(targetType);
        var content = provider.GetRequiredService(targetType) as FrameworkElement
            ?? throw new InvalidOperationException(
                $"Resolved service '{targetType.FullName}' is not a FrameworkElement.");
        NavigateCore(regionName, content, parameters);
    }

    private void NavigateCore(
        string regionName,
        FrameworkElement content,
        DialogParameters? parameters)
    {
        var host = GetRequiredHost(regionName);
        var adapter = (IContentRegionHostAdapter)
            RegionHostAdapterResolver.GetRequired(host);
        adapter.Present(host, content);
        NavigationAwareNotifier.Notify(content, parameters);
    }

    private ContentControl GetRequiredHost(string regionName)
    {
        var region = regionManager.GetRegion(regionName);
        if (region is ContentControl host && region is not Frame)
        {
            return host;
        }

        var actual = region?.GetType().FullName ?? "missing";
        throw new InvalidOperationException(
            $"Region '{regionName}' must be a non-Frame ContentControl but was '{actual}'.");
    }

    private static void ValidateContentType(Type targetType)
    {
        if (targetType == null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        if (!typeof(FrameworkElement).IsAssignableFrom(targetType) ||
            typeof(Page).IsAssignableFrom(targetType) ||
            typeof(Window).IsAssignableFrom(targetType))
        {
            throw new ArgumentException(
                $"Target type '{targetType.FullName}' must be a non-Page, non-Window FrameworkElement.",
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
