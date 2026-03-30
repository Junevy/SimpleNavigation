using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    public interface IPageAware
    {
        event Action<DialogParameters?> Receive;

        void OnNavigated(DialogParameters? parameters);
    }
}
