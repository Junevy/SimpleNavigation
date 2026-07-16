using SimpleNavigation.Common;
using SimpleNavigation.Interface.Awares;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Tests;

public sealed class FirstPage : Page
{
}

public sealed class SecondPage : Page
{
}

public sealed class FirstWindow : Window
{
}

public sealed class SecondWindow : Window
{
}

public sealed class TestContent : UserControl
{
}

public sealed class TestViewModel
{
}

public sealed class DialogViewModel
{
}

public class AwareDialogViewModel : IDialogAware
{
    private readonly IList<string>? calls;
    private readonly string name;

    public AwareDialogViewModel()
        : this(null, "view-model")
    {
    }

    public AwareDialogViewModel(IList<string>? calls, string name = "view-model")
    {
        this.calls = calls;
        this.name = name;
    }

    public int CallCount { get; private set; }

    public DialogParameters? Parameters { get; private set; }

    public Action<DialogParameters?>? RequestClose { get; set; }

    public virtual void OnNavigated(DialogParameters? parameters)
    {
        CallCount++;
        Parameters = parameters;
        calls?.Add(name);
    }
}

public class AwareWindow : Window, IDialogAware
{
    private readonly IList<string>? calls;
    private readonly string name;

    public AwareWindow()
        : this(null, "window")
    {
    }

    public AwareWindow(IList<string>? calls, string name = "window")
    {
        this.calls = calls;
        this.name = name;
    }

    public int CallCount { get; private set; }

    public DialogParameters? Parameters { get; private set; }

    public Action<DialogParameters?>? RequestClose { get; set; }

    public virtual void OnNavigated(DialogParameters? parameters)
    {
        CallCount++;
        Parameters = parameters;
        calls?.Add(name);
    }
}

public sealed class ThrowingAwareDialogViewModel : AwareDialogViewModel
{
    public override void OnNavigated(DialogParameters? parameters)
    {
        base.OnNavigated(parameters);
        throw new InvalidOperationException("dialog awareness failed");
    }
}

public sealed class ReplacingAwareDialogViewModel : AwareDialogViewModel
{
    public Action<DialogParameters?> Replacement { get; } = _ => { };

    public override void OnNavigated(DialogParameters? parameters)
    {
        base.OnNavigated(parameters);
        RequestClose = Replacement;
    }
}

public sealed class CancelClosingWindow : Window
{
    public bool CancelClosing { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = CancelClosing;
        base.OnClosing(e);
    }
}

public sealed class ReuseWindow : Window
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

public sealed class AwareContent : UserControl, INavigationAware
{
    public int CallCount { get; private set; }

    public DialogParameters? Parameters { get; private set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        CallCount++;
        Parameters = parameters;
    }
}

public sealed class ThrowingAwareContent : UserControl, INavigationAware
{
    public void OnNavigated(DialogParameters? parameters)
    {
        throw new InvalidOperationException("awareness failed");
    }
}

public sealed class ThrowingAwarePage : Page, INavigationAware
{
    public void OnNavigated(DialogParameters? parameters)
    {
        throw new InvalidOperationException("awareness failed");
    }
}
