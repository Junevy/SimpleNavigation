namespace SimpleNavigation.Common
{
    /// <summary>
    /// 导航参数对象
    /// </summary>
    public class NavigationParameter
    {
        public Dictionary<string, object> Parameters { get; } = [];

        public NavigationParameter(string key, object value)
        {
            if (key != null && value != null)
                Parameters[key] = value;
        }

        public NavigationParameter(Dictionary<string, object> keyValues)
        {
            if (keyValues != null)
                Parameters = keyValues;
        }

        public NavigationParameter(params object[] values)
        {
            for (int i = 0; i < values.Length -1; i++)
            {
                Parameters[i.ToString()] = values[i];
            }
        }

        /// <summary>
        /// 设置导航参数，允许覆盖已有的参数值。
        /// </summary>
        /// <param name="key">参数的Key</param>
        /// <param name="value">参数的Value</param>
        /// <returns></returns>
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

        /// <summary>
        /// 根据Key获取导航参数
        /// </summary>
        /// <typeparam name="TType">需要的返回值类型</typeparam>
        /// <param name="key">参数对应的Key</param>
        /// <returns></returns>
        public TType? Get<TType>(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                return Parameters.TryGetValue(key, out var value) ? (TType)value : default;
            return default;
        }
    }
}
