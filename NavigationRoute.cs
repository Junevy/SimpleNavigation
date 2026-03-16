using System.Windows.Controls;

namespace SimpleNavigation
{
    internal class NavigationRoute(Type pageType, Func<Page> factory, NavigationOptions options)
    {
        private readonly Func<Page> factory = factory;
        //private readonly NavigationOptions.PageMode mode = mode;
        //private readonly NavigationOptions.KeepHistory keepHistory = keepHistory;


        //public NavigationOptions.PageMode AllowMulti => mode;
        //public NavigationOptions.KeepHistory KeepHistory => keepHistory;
        public NavigationOptions Options { get; } = options;


        public Type PageType { get; } = pageType;

        

        private Page? cachePage;

        public Page GetPage()
        {
            if (Options.AllowMulti == NavigationOptions.PageMode.Singleton)
                return cachePage ??= factory();

            return factory();
        }
    }
}
