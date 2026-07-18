using SimpleNavigation.Interface.Adapters;
using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common.Adapters
{
    internal class TabControlRegionAdapter : IContentRegionHostAdapter
    {
        public RegionHostKind Kind => RegionHostKind.Content;

        public bool CanHandle(FrameworkElement region) => region is TabControl;

        public void Present(FrameworkElement host, FrameworkElement content)
        {
            if (host is not TabControl tabControl || content is not UserControl view)
            {
                throw new ArgumentException(
                    $"Host type '{host.GetType().FullName}' must be a TabControl.",
                    nameof(host));
            }

            host.Dispatcher.VerifyAccess();

            if (tabControl.ItemsSource == null)
            {
                tabControl.Items.Add(content);
            }
            else
            {
                if (tabControl.ItemsSource is IList list)
                {
                    //list.Add(new TabItem() { Header = "new tab", Content = view});
                    if (list.Count <= 0)
                        throw new ArgumentException("The tab control must be keep at least 1 item");

                    var targetType = list[0]?.GetType();
                    object instance = Activator.CreateInstance(targetType);
                    if (instance != null)
                    {
                        FieldInfo contentFi = targetType.GetField("ContentProperty", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        DependencyProperty contentDp = (DependencyProperty)contentFi.GetValue(null);

                        FieldInfo headerFi = targetType.GetField("HeaderProperty", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        DependencyProperty headerDp = (DependencyProperty)headerFi.GetValue(null);

                        var header = content.GetType().Name.Replace("View", "");

                        ((DependencyObject)instance).SetValue(headerDp, header);
                        ((DependencyObject)instance).SetValue(contentDp, content);
                        list.Add(instance);
                    }
                }
            }
        }
    }
}
