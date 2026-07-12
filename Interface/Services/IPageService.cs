using SimpleNavigation.Common;
using System.Windows.Controls;

namespace SimpleNavigation.Interface.Services
{
    /// <summary>
    /// Page µ¼º½½Ó¿Ú
    /// </summary>
    public interface IPageService
    {
        void Navigate<TPage>(string regionName, DialogParameters? parameters = null) where TPage : Page;

        void Navigate(string regionName, Type targetType, DialogParameters? parameters = null);

        void Navigate(string regionName, string key, DialogParameters? parameters = null);

        void GoBack(string regionName);

        [Obsolete("Use GoBack instead.")]
        void Goback(string regionName);
    }
}