using SimpleNavigation.Interface.Adapters;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common.Adapters;

internal sealed class FrameRegionAdapter : IPageRegionHostAdapter
{
    public RegionHostKind Kind => RegionHostKind.Page;

    public bool CanHandle(FrameworkElement region) => region is Frame;

    public bool Navigate(Frame frame, Page page)
    {
        frame.Dispatcher.VerifyAccess();
        return frame.Navigate(page);
    }

    public bool CanGoBack(Frame frame)
    {
        frame.Dispatcher.VerifyAccess();
        return frame.CanGoBack;
    }

    public void GoBack(Frame frame)
    {
        frame.Dispatcher.VerifyAccess();
        frame.GoBack();
    }
}
