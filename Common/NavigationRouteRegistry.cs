namespace SimpleNavigation.Common
{
    /// <summary>
    /// 被导航对象的类型
    /// </summary>
    internal enum NavigationRouteKind
    {
        Page,
        Content,
    }

    /// <summary>
    /// 导航对象
    /// </summary>
    internal sealed class NavigationRouteRegistration
    {
        public NavigationRouteKind Kind { get; }

        public string Key { get; }

        public Type TargetType { get; }

        public NavigationRouteRegistration(NavigationRouteKind kind, string key, Type targetType)
        {
            Kind = kind;
            Key = key;
            TargetType = targetType;
        }
    }

    internal sealed class NavigationRouteRegistry
    {
        private readonly IReadOnlyDictionary<string, Type> pages;
        private readonly IReadOnlyDictionary<string, Type> contents;

        public NavigationRouteRegistry(IEnumerable<NavigationRouteRegistration> registrations)
        {
            pages = BuildRoute(registrations, NavigationRouteKind.Page);
            contents = BuildRoute(registrations, NavigationRouteKind.Content);
        }

        public Type GetRequiredPageType(string key)  => GetRequiredTarget(pages, key, "page");

        public Type GetRequiredContentType(string key) => GetRequiredTarget(contents, key, "content");

        /// <summary>
        /// Build 导航路由
        /// </summary>
        /// <param name="registrations">被导航对象</param>
        /// <param name="kind">被导航对象的类型</param>
        /// <returns></returns>
        private IReadOnlyDictionary<string, Type> BuildRoute(
            IEnumerable<NavigationRouteRegistration> registrations,
            NavigationRouteKind kind)
        {
            var routes = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var registration in registrations.Where(item => item.Kind == kind))
                routes.Add(registration.Key, registration.TargetType);

            return routes;
        }

        private Type GetRequiredTarget(IReadOnlyDictionary<string, Type> routes, string key, string category)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "Route key cannot be null or whitespace.",
                    nameof(key));
            }

            if (routes.TryGetValue(key, out var targetType))
                return targetType;

            throw new KeyNotFoundException(
                $"No {category} route is registered for key '{key}'.");
        }
    }
}
