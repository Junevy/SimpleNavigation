using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation
{
    /// <summary>
    /// 维护区域功能
    /// </summary>
    public static class RegionService
    {
        // 允许在XAML中使用RegionService注册导航区域，注册完成后会触发<see cref="RegionRegisted"/>事件，导航服务会订阅该事件以完成区域的注册。
        public static event Action<string, Frame>? RegionRegisted;

        public static string GetRegionName(DependencyObject obj)
        {
            return (string)obj.GetValue(RegionNameProperty);
        }

        public static void SetRegionName(DependencyObject obj, string value)
        {
            obj.SetValue(RegionNameProperty, value);
        }

        // 使用附加属性的方式允许在XAML中为控件指定RegionName，RegionService会监听该属性的变化以完成导航区域的注册。
        public static readonly DependencyProperty RegionNameProperty =
            DependencyProperty.RegisterAttached("RegionName", typeof(string), typeof(RegionService), new PropertyMetadata("", OnRegionNameChanged));


        private static void OnRegionNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Frame frame) throw new InvalidOperationException("RegionName can only be attached to Frame controls.");

            var regionName = e.NewValue as string;

            if (string.IsNullOrWhiteSpace(regionName)) throw new ArgumentException("Region name cannot be null or whitespace.", nameof(regionName));

            RegionRegisted?.Invoke(regionName, frame);
        }
    }
}
