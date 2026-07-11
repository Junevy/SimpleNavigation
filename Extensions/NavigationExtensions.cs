using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using SimpleNavigation.Services;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Extensions
{
    public static class NavigationExtensions
    {
        public static IServiceCollection RegisterNavigationService(
            this IServiceCollection serviceCollection)
        {
            serviceCollection.TryAddSingleton<IDialogService, DialogService>();
            serviceCollection.TryAddSingleton<IPageService, PageService>();
            serviceCollection.TryAddSingleton<IDialogManager, DialogManager>();
            serviceCollection.TryAddSingleton<IRegionManager, RegionManager>();
            serviceCollection.TryAddSingleton<NavigationRouteRegistry>(provider =>
                new NavigationRouteRegistry(
                    provider.GetServices<NavigationRouteRegistration>()));

            return serviceCollection;
        }

        public static IServiceCollection AddPage<TPage>(
            this IServiceCollection services,
            string key)
            where TPage : Page
        {
            AddRoute(services, NavigationRouteKind.Page, key, typeof(TPage));
            services.TryAddTransient<TPage>();
            return services;
        }

        public static IServiceCollection AddPage<TPage, TViewModel>(
            this IServiceCollection services)
            where TPage : Page
            where TViewModel : class
        {
            services.TryAddTransient<TPage>();
            services.TryAddTransient<TViewModel>();
            return services;
        }

        public static IServiceCollection AddPage<TPage, TViewModel>(
            this IServiceCollection services,
            string key)
            where TPage : Page
            where TViewModel : class
        {
            AddRoute(services, NavigationRouteKind.Page, key, typeof(TPage));
            services.TryAddTransient<TPage>();
            services.TryAddTransient<TViewModel>();
            return services;
        }

        public static IServiceCollection AddContent<TView>(
            this IServiceCollection services,
            string key)
            where TView : FrameworkElement
        {
            ValidateContentType(typeof(TView));
            AddRoute(services, NavigationRouteKind.Content, key, typeof(TView));
            services.TryAddTransient<TView>();
            return services;
        }

        public static IServiceCollection AddContent<TView, TViewModel>(
            this IServiceCollection services)
            where TView : FrameworkElement
            where TViewModel : class
        {
            ValidateContentType(typeof(TView));
            services.TryAddTransient<TView>();
            services.TryAddTransient<TViewModel>();
            return services;
        }

        public static IServiceCollection AddContent<TView, TViewModel>(
            this IServiceCollection services,
            string key)
            where TView : FrameworkElement
            where TViewModel : class
        {
            ValidateContentType(typeof(TView));
            AddRoute(services, NavigationRouteKind.Content, key, typeof(TView));
            services.TryAddTransient<TView>();
            services.TryAddTransient<TViewModel>();
            return services;
        }

        private static void AddRoute(
            IServiceCollection services,
            NavigationRouteKind kind,
            string key,
            Type targetType)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "Route key cannot be null or whitespace.",
                    nameof(key));
            }

            var duplicate = services.Any(descriptor =>
                descriptor.ServiceType == typeof(NavigationRouteRegistration) &&
                descriptor.ImplementationInstance is NavigationRouteRegistration route &&
                route.Kind == kind &&
                string.Equals(route.Key, key, StringComparison.Ordinal));

            if (duplicate)
            {
                throw new ArgumentException(
                    $"A {kind.ToString().ToLowerInvariant()} route with key '{key}' is already registered.",
                    nameof(key));
            }

            services.AddSingleton(
                new NavigationRouteRegistration(kind, key, targetType));
        }

        private static void ValidateContentType(Type targetType)
        {
            if (typeof(Page).IsAssignableFrom(targetType) ||
                typeof(Window).IsAssignableFrom(targetType))
            {
                throw new ArgumentException(
                    $"Content type '{targetType.FullName}' cannot derive from Page or Window.",
                    nameof(targetType));
            }
        }
    }
}
