using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider provider;
        public DialogService(IServiceProvider provider)
        {
            this.provider = provider;
        }

        public void Show<T>(DialogParameters? parameters = null) where T : Window
        {
            var window = provider.GetRequiredService<T>();

            if (window.DataContext is IDialogAware vm)
                vm.OnNavigated(parameters);

            if (window is IDialogAware w)
                w.OnNavigated(parameters);

                window.Show();
        }

        public DialogParameters? ShowDialog<T>(DialogParameters? parameters = null) where T : Window
        {
            var window = provider.GetRequiredService<T>();
            DialogParameters? result = null;

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

            window.ShowDialog();

            return result;
        }
    }
}
