using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common;

internal sealed class ContentControlRegionAdapter : IContentRegionHostAdapter
{
    public RegionHostKind Kind => RegionHostKind.Content;

    public bool CanHandle(FrameworkElement region)
    {
        return region is ContentControl && region is not Frame;
    }

    public void Present(FrameworkElement host, FrameworkElement content)
    {
        if (host is not ContentControl contentControl || host is Frame)
        {
            throw new ArgumentException(
                $"Host type '{host.GetType().FullName}' must be a non-Frame ContentControl.",
                nameof(host));
        }

        host.Dispatcher.VerifyAccess();
        contentControl.Content = content;
    }
}
