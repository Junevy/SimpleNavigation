using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Windows;

namespace SimpleNavigation.Services
{
    /// <summary>
    /// 窗口服务实现类
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly IDialogManager dialogManager;

        public DialogService(IDialogManager dialogManager)
        {
            this.dialogManager = dialogManager;
        }

        public void Show<T>(DialogParameters? parameters = null) where T : Window
        {
            var window = dialogManager.GetDialogWindow<T>();

            if (window == null)
                return;

            if (window.DataContext is IDialogAware vm)
            {
                vm.OnNavigated(parameters);
                vm.RequestClose = (p) => window.Close();
            }

            if (window is IDialogAware w)
            {
                w.OnNavigated(parameters);
                w.RequestClose = (p) => window.Close();
            }

            window.Show();
            window.Activate();

        }

        public DialogParameters? ShowDialog<T>(DialogParameters? parameters = null) where T : Window
        {
            var window = dialogManager.GetDialogWindow<T>();
            if (window == null)
                return null;

            DialogParameters? result = null;

            window.Closing += (s, e) =>
            {
                e.Cancel = true;
                if (s is IDialogAware dialogAware)
                {
                    dialogAware.RequestClose?.Invoke(result);
                }

                if (s is Window w && w.DataContext is IDialogAware dialogVmAware)
                {
                    dialogVmAware.RequestClose?.Invoke(result);
                }
            };

            if (window.DataContext is IDialogAware vm)
            {
                vm.OnNavigated(parameters);
                vm.RequestClose = (p) =>
                {
                    result = p;
                    window.Close();
                };
            }

            if (window is IDialogAware w)
            {
                w.OnNavigated(parameters);
                w.RequestClose = (p) =>
                {
                    result = p;
                    window.Close();
                };
            }

            try
            {
                window.ShowDialog();
                window.Activate();
            }
            finally
            {
                if (window.DataContext is IDialogAware cleanupVm)
                    cleanupVm.RequestClose = null;
                else if (window is IDialogAware cleanupW)
                    cleanupW.RequestClose = null;

            }
            return result;
        }
    }
}
