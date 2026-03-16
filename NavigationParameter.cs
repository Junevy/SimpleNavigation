namespace SimpleNavigation
{
    public class NavigationParameter
    {
        public Dictionary<string, object> Parameters { get; } = [];

        public bool Set(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key) && value != null)
            {
                // 允许覆盖已有的value
                Parameters[key] = value;
                return true;
            }
            return false;
        }

        public TType? Get<TType>(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                return Parameters.TryGetValue(key, out var value) ? (TType)value : default;
            return default;
        }
    }
}
