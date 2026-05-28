using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Collections.Concurrent;
using System.Windows.Controls;

namespace SimpleNavigation.Services
{
    public class PageService : IPageService
    {
        private readonly ConcurrentDictionary<string, Frame> regions = new();
        private readonly IServiceProvider provider;

        public PageService(IServiceProvider provider)
        {
            this.provider = provider;
            RegionService.RegionRegisted += (regionName, frame) => RegisterRegion(regionName, frame);
        }

        public void RegisterRegion(string regionName, Frame frame)
        {
            if (!string.IsNullOrWhiteSpace(regionName) && frame != null)
                regions[regionName] = frame;
        }

        public Frame? GetRegion(string regionName)
        {
            regions.TryGetValue(regionName, out var frame);
            return frame;
        }

        public void Goback(string region)
        {
            if (regions.TryGetValue(region, out var frame))
            {
                if (frame.CanGoBack)
                    frame.GoBack();
            }
        }

        public void Navigate<T>(string regionName, DialogParameters? parameters = null) where T : Page
        {
            var region = GetRegion(regionName);
            if (region != null)
            {
                var page = provider.GetRequiredService<T>();

                //var oldContent = region.Content;

                region.Navigate(page);

                if (page.DataContext is IPageAware pA && parameters != null)
                {
                    pA.OnNavigated(parameters);
                }
            }
        }

        public void Navigate(string regionName, Type targetType, DialogParameters? parameters = null)
        {
            if (targetType.IsSubclassOf(typeof(Page)))
            {
                var region = GetRegion(regionName);
                if (region != null)
                {
                    var page = provider.GetRequiredService(targetType) as Page;
                    region.Navigate(page);
                    if (page?.DataContext is IPageAware pA && parameters != null)
                    {
                        pA.OnNavigated(parameters);
                    }
                }
            }
        }
    }
}
