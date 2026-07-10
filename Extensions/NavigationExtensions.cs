using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using SimpleNavigation.Services;

namespace SimpleNavigation.Extensions
{
   public static class NavigationExtensions
   {
       public static IServiceCollection RegisterNavigationService(this IServiceCollection serviceCollection) 
       {
           serviceCollection.TryAddSingleton<IDialogService, DialogService>();
           serviceCollection.TryAddSingleton<IPageService, PageService>();
           serviceCollection.TryAddSingleton<IDialogManager, DialogManager>();

           return serviceCollection;
       }
   }
}
