using System.Windows;

namespace SimpleNavigation.Interface
{
    public interface IDialogManager
    {
        ///
        public T? GetDialogWindow<T>() where T : Window;
    }
}
