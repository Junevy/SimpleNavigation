using SimpleNavigation.Common;
using System.Windows.Controls;

namespace SimpleNavigation.Interface
{
    public interface IPageService
    {
        void Navigate<T>(string regionName, DialogParameters? parameters = null) where T : Page;

        Frame? GetRegion(string regionName);

        void Goback(string region);
    }
}
