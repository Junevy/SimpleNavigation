using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Common.Adapters;
using SimpleNavigation.Interface.Adapters;
using SimpleNavigation.Interface.Managers;
using SimpleNavigation.Interface.Services;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Services
{
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

        public void Navigate<TContent>(string regionName, DialogParameters? parameters = null)
            where TContent : FrameworkElement
        {
            ValidateRegionName(regionName);
            ValidateContentType(typeof(TContent));
            NavigateCore(regionName, provider.GetRequiredService<TContent>(), parameters);
        }

        public void Navigate(string regionName, Type targetType, DialogParameters? parameters = null)
        {
            ValidateRegionName(regionName);
            ValidateContentType(targetType);
            var content = provider.GetRequiredService(targetType) as FrameworkElement
                ?? throw new InvalidOperationException(
                    $"Resolved service '{targetType.FullName}' is not a FrameworkElement.");
            NavigateCore(regionName, content, parameters);
        }

        public void Navigate(string regionName, string key, DialogParameters? parameters = null)
        {
            ValidateRegionName(regionName);
            var targetType = routes.GetRequiredContentType(key);
            ValidateContentType(targetType);
            var content = provider.GetRequiredService(targetType) as FrameworkElement
                ?? throw new InvalidOperationException(
                    $"Resolved service '{targetType.FullName}' is not a FrameworkElement.");
            NavigateCore(regionName, content, parameters);
        }

        /// <summary>
        /// 导航功能的核心实现方法
        /// </summary>
        /// <param name="regionName">指定的 Region 名称</param>
        /// <param name="content">需要导航的UI控件或内容</param>
        /// <param name="parameters">传递的参数</param>
        private void NavigateCore(string regionName, FrameworkElement content, DialogParameters? parameters)
        {
            var host = GetRequiredHost(regionName);
            var hostAdapter = RegionHostAdapterResolver.GetRequired(host);
            if (hostAdapter is not IContentRegionHostAdapter adapter)
                throw CreateInvalidHostException(regionName, host);

            adapter.Present(host, content);
            NavigationAwareNotifier.Notify(content, parameters);
        }

        /// <summary>
        /// 获取指定 Region 的实例
        /// </summary>
        /// <param name="regionName">Region 名称</param>
        /// <returns>Region 实例</returns>
        /// <exception cref="CreateInvalidHostException">该宿主未注册或查找失败</exception>
        private FrameworkElement GetRequiredHost(string regionName)
        {
            var region = regionManager.GetRegion(regionName);
            if (region != null)
                return region;

            throw CreateInvalidHostException(regionName, null);
        }

        /// <summary>
        /// 自定义异常：宿主未注册或查找失败
        /// </summary>
        /// <param name="regionName">Region 名称</param>
        /// <param name="host">宿主实例</param>
        /// <returns>CreateInvalidHostException异常</returns>
        private static InvalidOperationException CreateInvalidHostException(
            string regionName,
            FrameworkElement? host)
        {
            var actual = host?.GetType().FullName ?? "missing";
            return new InvalidOperationException(
                $"Region '{regionName}' must be a non-Frame ContentControl or another host " +
                $"with a content navigation adapter but was '{actual}'.");
        }

        /// <summary>
        /// 校验需要导航的 UI控件或内容 是否符合约束
        /// </summary>
        /// <param name="targetType">需要导航的内容类型</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        private static void ValidateContentType(Type targetType)
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(targetType);
#elif NET46_OR_GREATER
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
#endif
            if (!typeof(FrameworkElement).IsAssignableFrom(targetType) 
                || typeof(Page).IsAssignableFrom(targetType) 
                || typeof(Window).IsAssignableFrom(targetType))
            {
                throw new ArgumentException(
                    $"Target type '{targetType.FullName}' must be a non-Page, non-Window FrameworkElement.",
                    nameof(targetType));
            }
        }

        /// <summary>
        /// 校验 Region 名称
        /// </summary>
        /// <param name="regionName"></param>
        /// <exception cref="ArgumentException"></exception>
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
}


