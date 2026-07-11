using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Windows.Controls;

namespace SimpleNavigation.Tests;

public sealed class FirstPage : Page
{
}

public sealed class SecondPage : Page
{
}

public sealed class TestContent : UserControl
{
}

public sealed class TestViewModel
{
}

public sealed class AwareViewModel : INavigationAware
{
    public int CallCount { get; private set; }

    public DialogParameters? Parameters { get; private set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        CallCount++;
        Parameters = parameters;
    }
}

public sealed class AwarePage : Page, INavigationAware
{
    public int CallCount { get; private set; }

    public DialogParameters? Parameters { get; private set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        CallCount++;
        Parameters = parameters;
    }
}

public sealed class ThrowingAwarePage : Page, INavigationAware
{
    public void OnNavigated(DialogParameters? parameters)
    {
        throw new InvalidOperationException("awareness failed");
    }
}
