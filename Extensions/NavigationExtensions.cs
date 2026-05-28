using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using SimpleNavigation.Services;

namespace SimpleNavigation.Extensions
{
   public static class NavigationExtensions
   {
       public static IServiceCollection RegisterNavigationService(this IServiceCollection serviceCollection) 
       {
           serviceCollection.AddTransient<IDialogService, DialogService>();
           serviceCollection.AddTransient<IPageService, PageService>();
           serviceCollection.AddTransient<IDialogManager, DialogManager>();

           return serviceCollection;
       }
   }
}
