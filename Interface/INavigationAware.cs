using SimpleNavigation.Common;

namespace SimpleNavigation.Interface;

public interface INavigationAware
{
    void OnNavigated(DialogParameters? parameters);
}
