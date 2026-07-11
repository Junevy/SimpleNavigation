namespace SimpleNavigation.Common
{
    internal enum NavigationRouteKind
    {
        Page,
        Content,
    }

    internal sealed class NavigationRouteRegistration
    {
        public NavigationRouteRegistration(
            NavigationRouteKind kind,
            string key,
            Type targetType)
        {
            Kind = kind;
            Key = key;
            TargetType = targetType;
        }

        public NavigationRouteKind Kind { get; }

        public string Key { get; }

        public Type TargetType { get; }
    }

    internal sealed class NavigationRouteRegistry
    {
        private readonly IReadOnlyDictionary<string, Type> pages;
        private readonly IReadOnlyDictionary<string, Type> contents;

        public NavigationRouteRegistry(IEnumerable<NavigationRouteRegistration> registrations)
        {
            pages = Build(registrations, NavigationRouteKind.Page);
            contents = Build(registrations, NavigationRouteKind.Content);
        }

        public Type GetRequiredPageType(string key) =>
            GetRequired(pages, key, "page");

        public Type GetRequiredContentType(string key) =>
            GetRequired(contents, key, "content");

        private static IReadOnlyDictionary<string, Type> Build(
            IEnumerable<NavigationRouteRegistration> registrations,
            NavigationRouteKind kind)
        {
            var routes = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var registration in registrations.Where(item => item.Kind == kind))
            {
                routes.Add(registration.Key, registration.TargetType);
            }

            return routes;
        }

        private static Type GetRequired(
            IReadOnlyDictionary<string, Type> routes,
            string key,
            string category)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "Route key cannot be null or whitespace.",
                    nameof(key));
            }

            if (routes.TryGetValue(key, out var targetType))
            {
                return targetType;
            }

            throw new KeyNotFoundException(
                $"No {category} route is registered for key '{key}'.");
        }
    }
}
