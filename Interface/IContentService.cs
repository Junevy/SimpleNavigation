using SimpleNavigation.Common;
using System.Windows;

namespace SimpleNavigation.Interface;

public interface IContentService
{
    void Navigate<TContent>(
        string regionName,
        DialogParameters? parameters = null)
        where TContent : FrameworkElement;

    void Navigate(
        string regionName,
        Type targetType,
        DialogParameters? parameters = null);

    void Navigate(
        string regionName,
        string key,
        DialogParameters? parameters = null);
}
