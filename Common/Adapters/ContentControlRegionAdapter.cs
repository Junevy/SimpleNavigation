using SimpleNavigation.Interface.Adapters;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common.Adapters;

internal sealed class ContentControlRegionAdapter : IContentRegionHostAdapter
{
    public RegionHostKind Kind => RegionHostKind.Content;

    public bool CanHandle(FrameworkElement region)
        => region is ContentControl && region is not Frame;

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
