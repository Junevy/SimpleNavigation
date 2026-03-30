using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    public interface IDialogAware
    {
        Action<DialogParameters?> RequestClose { get; set; }

        void OnNavigated(DialogParameters? parameters);
    }
}
