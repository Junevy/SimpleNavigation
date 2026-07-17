using SimpleNavigation.Interface.Adapters;
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
            if (host is not TabControl tabControl)
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
            //else
            //{
            //    if (tabControl.ItemsSource is IList list)
            //    {
            //        list.Add(list);
            //    }

            //    if (tabControl.ItemsSource is IEnumerable enums)
            //    {
            //        enums.
            //    }
            //}
        }
    }
}
