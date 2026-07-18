# DialogService Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add transient Window registration, independent Dialog routes, key-based Show/ShowDialog/Close operations, safe Window recreation, and accurate README guidance.

**Architecture:** Extend the existing `NavigationRouteRegistry` with an independent Dialog map. `DialogManager` owns weak references to current Window instances by Window type and exposes separate get-or-create and get-existing paths. `DialogService` resolves generic/Type/key targets through ordinary DI, delegates instance lifecycle to `DialogManager`, and keeps all Window operations on the Window Dispatcher.

**Tech Stack:** C# 13, WPF, Microsoft.Extensions.DependencyInjection 6/8, xUnit 2.5.3, .NET Framework 4.8, .NET 8 Windows.

---

## Scope and baseline

The current workspace contains user-owned uncommitted changes in `Services/Region.cs` and `SimpleNavigation.csproj`; preserve them. The current baseline also has unrelated failures caused by the user's singleton Page/Content registrations, Region namespace reorganization, and stale reflection test names. Dialog implementation must not revert those changes. Use the stable SDK invocation:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" <command>
```

Existing public namespaces are currently split as follows:

- `Interface.Services`: `IDialogService`, `IPageService`, `IContentService`.
- `Interface.Managers`: `IDialogManager`, `IRegionManager`.
- `Interface.Awares`: `IDialogAware`, `INavigationAware`.
- `Common.Managers`: `DialogManager`, `RegionManager`.

Do not stage `Services/Region.cs`, `SimpleNavigation.csproj`, or the untracked design-copy file.

## File map

- Modify `Common/NavigationRouteRegistry.cs`: add the Dialog route kind, Dialog map, and `GetRequiredDialogType`.
- Modify `Extensions/NavigationExtensions.cs`: add the three `AddWindow` overloads and Dialog route registration.
- Modify `Interface/Managers/IDialogManager.cs`: add type-based get-or-create and get-existing operations.
- Modify `Common/Managers/DialogManager.cs`: maintain weak current Window instances by exact type and remove only the exact closed instance.
- Modify `Interface/Services/IDialogService.cs`: add generic, Type, and key Show/ShowDialog/Close overloads.
- Modify `Services/DialogService.cs`: implement all resolution paths, awareness, non-modal display, modal result handling, and bool Close behavior.
- Modify `Tests/SimpleNavigation.Tests/TestTypes.cs`: add Window, dialog-aware ViewModel, and close-cancel fixtures.
- Modify `Tests/SimpleNavigation.Tests/NavigationExtensionsTests.cs`: add Dialog route and AddWindow coverage; preserve existing user changes.
- Create `Tests/SimpleNavigation.Tests/DialogManagerTests.cs`: test instance creation, reuse, exact Closed removal, and no-create lookup.
- Create `Tests/SimpleNavigation.Tests/DialogServiceTests.cs`: test Show, ShowDialog, Close, key resolution, awareness, Dispatcher, and error behavior.
- Modify `README.md`: document AddWindow, all Dialog service overloads, key-based close, transient recreation, and the repaired modal behavior.

## Task 1: Add Dialog route registration and Window extensions

**Files:**

- Modify: `Common/NavigationRouteRegistry.cs`
- Modify: `Extensions/NavigationExtensions.cs`
- Modify: `Tests/SimpleNavigation.Tests/TestTypes.cs`
- Modify: `Tests/SimpleNavigation.Tests/NavigationExtensionsTests.cs`

- [ ] **Step 1: Add Window and ViewModel fixtures first**

Add these test-only types to `TestTypes.cs`:

```csharp
public sealed class FirstWindow : Window
{
}

public sealed class SecondWindow : Window
{
}

public sealed class DialogViewModel
{
}
```

Use `System.Windows` and the existing `SimpleNavigation.Interface.Awares` namespace only where a fixture needs awareness.

- [ ] **Step 2: Add failing registration tests**

Add tests to `NavigationExtensionsTests.cs`:

```csharp
[Fact]
public void AddWindowKeyRegistersTransientWindowAndDialogRoute()
{
    StaTest.Run(() =>
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        services.AddWindow<FirstWindow>("first");
        using var provider = services.BuildServiceProvider();

        Assert.NotSame(
            provider.GetRequiredService<FirstWindow>(),
            provider.GetRequiredService<FirstWindow>());
    });
}

[Fact]
public void AddWindowDoubleGenericRegistersWindowAndViewModelWithoutDataContext()
{
    StaTest.Run(() =>
    {
        var services = new ServiceCollection();
        services.AddWindow<FirstWindow, DialogViewModel>();
        using var provider = services.BuildServiceProvider();

        var window = provider.GetRequiredService<FirstWindow>();
        Assert.Null(window.DataContext);
        Assert.NotSame(
            provider.GetRequiredService<DialogViewModel>(),
            provider.GetRequiredService<DialogViewModel>());
    });
}

[Fact]
public void DialogKeysAreIndependentFromPageAndContentKeys()
{
    var services = new ServiceCollection();
    services.AddPage<FirstPage>("main");
    services.AddContent<TestContent>("main");
    services.AddWindow<FirstWindow>("main");
}

[Fact]
public void DuplicateDialogKeyIsRejectedUsingOrdinalMatching()
{
    var services = new ServiceCollection();
    services.AddWindow<FirstWindow>("main");

    Assert.Throws<ArgumentException>(
        () => services.AddWindow<SecondWindow>("main"));
}
```

- [ ] **Step 3: Run the focused tests and verify RED**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter "FullyQualifiedName~AddWindow|FullyQualifiedName~DialogKey|FullyQualifiedName~DuplicateDialog" --no-restore
```

Expected: compile failures for missing `AddWindow` and missing Dialog route support. Do not change production code before observing this failure.

- [ ] **Step 4: Extend the route registry**

Change the internal enum and registry with the following behavior:

```csharp
internal enum NavigationRouteKind
{
    Page,
    Content,
    Dialog,
}

private readonly IReadOnlyDictionary<string, Type> dialogs;

public Type GetRequiredDialogType(string key) =>
    GetRequiredTarget(dialogs, key, "dialog");
```

Build `dialogs` with the same `StringComparer.Ordinal` and category filter used by the Page and Content maps. Do not combine the dictionaries.

- [ ] **Step 5: Add the three Window extensions**

Add these methods to `NavigationExtensions`:

```csharp
public static IServiceCollection AddWindow<TWindow>(
    this IServiceCollection services,
    string key)
    where TWindow : Window
{
    AddRoute(services, NavigationRouteKind.Dialog, key, typeof(TWindow));
    services.TryAddTransient<TWindow>();
    return services;
}

public static IServiceCollection AddWindow<TWindow, TViewModel>(
    this IServiceCollection services)
    where TWindow : Window
    where TViewModel : class
{
    services.TryAddTransient<TWindow>();
    services.TryAddTransient<TViewModel>();
    return services;
}

public static IServiceCollection AddWindow<TWindow, TViewModel>(
    this IServiceCollection services,
    string key)
    where TWindow : Window
    where TViewModel : class
{
    AddRoute(services, NavigationRouteKind.Dialog, key, typeof(TWindow));
    services.TryAddTransient<TWindow>();
    services.TryAddTransient<TViewModel>();
    return services;
}
```

Reuse the existing whitespace validation and category-local ordinal duplicate detection in `AddRoute`. Do not set `DataContext`.

- [ ] **Step 6: Add route lookup and registration-order tests**

Extend the existing reflection helper in `NavigationExtensionsTests` to call `GetRequiredDialogType`. Test a Dialog route registered before `RegisterNavigationService`, one registered after it but before `BuildServiceProvider`, ordinal casing, unknown key, and invalid key. Tests that already pass because the implementation is present are coverage additions; report them honestly rather than claiming a false RED cycle.

- [ ] **Step 7: Run focused GREEN and commit Task 1**

Run the focused extension tests on both targets:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net48 --filter "FullyQualifiedName~AddWindow|FullyQualifiedName~DialogKey|FullyQualifiedName~DuplicateDialog|FullyQualifiedName~DialogRoute" --no-restore
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter "FullyQualifiedName~AddWindow|FullyQualifiedName~DialogKey|FullyQualifiedName~DuplicateDialog|FullyQualifiedName~DialogRoute" --no-restore
```

Commit only the route/extension and test changes:

```powershell
git add Common/NavigationRouteRegistry.cs Extensions/NavigationExtensions.cs Tests/SimpleNavigation.Tests/NavigationExtensionsTests.cs Tests/SimpleNavigation.Tests/TestTypes.cs
git commit -m "feat: add dialog window registration routes"
```

## Task 2: Refactor DialogManager instance lifecycle

**Files:**

- Modify: `Interface/Managers/IDialogManager.cs`
- Modify: `Common/Managers/DialogManager.cs`
- Create: `Tests/SimpleNavigation.Tests/DialogManagerTests.cs`

- [ ] **Step 1: Write failing manager tests**

Add tests that express the desired type-based operations:

```csharp
[Fact]
public void GetOrCreateReusesLiveWindowAndGetExistingDoesNotCreate()
{
    StaTest.Run(() =>
    {
        var expected = new FirstWindow();
        var services = new ServiceCollection();
        services.AddSingleton(expected);
        using var provider = services.BuildServiceProvider();
        var manager = new DialogManager(provider);

        Assert.Null(manager.GetExistingWindow(typeof(FirstWindow)));
        Assert.Same(expected, manager.GetOrCreateWindow(typeof(FirstWindow)));
        Assert.Same(expected, manager.GetExistingWindow(typeof(FirstWindow)));
    });
}

[Fact]
public void ClosedWindowIsRemovedAndNextGetOrCreateUsesDIAgain()
{
    StaTest.Run(() =>
    {
        var created = 0;
        var services = new ServiceCollection();
        services.AddTransient<FirstWindow>(_ =>
        {
            created++;
            return new FirstWindow();
        });
        using var provider = services.BuildServiceProvider();
        var manager = new DialogManager(provider);

        var first = manager.GetOrCreateWindow(typeof(FirstWindow));
        first.Show();
        first.Close();
        Assert.Null(manager.GetExistingWindow(typeof(FirstWindow)));

        var second = manager.GetOrCreateWindow(typeof(FirstWindow));
        Assert.NotSame(first, second);
        Assert.Equal(2, created);
        second.Show();
        second.Close();
    });
}
```

Every created Window is shown and closed on the STA thread so the real WPF `Closed` event drives cache removal. Do not add production-only test hooks.

- [ ] **Step 2: Run manager tests and verify RED**

Run the new test class on `net8.0-windows`; expected failure is missing `GetExistingWindow`/`GetOrCreateWindow` methods or the old type cache behavior.

- [ ] **Step 3: Implement type-based manager operations**

Refactor `IDialogManager` to expose:

```csharp
Window GetOrCreateWindow(Type windowType);
Window? GetExistingWindow(Type windowType);
```

`DialogManager` must:

- Validate that `windowType` is non-null and derives from `Window`.
- Use ordinary `provider.GetRequiredService(windowType)` only in `GetOrCreateWindow`.
- Store `WeakReference<Window>` by exact `Type`.
- Subscribe once to each Window's `Closed` event.
- Remove a cache entry only when both cached type and cached Window reference match the closed sender.
- Return null from `GetExistingWindow` for an absent or dead weak reference without resolving DI.

Keep the generic `GetDialogWindow<T>` only if existing consumers require source compatibility; implement it as a thin call to `GetOrCreateWindow(typeof(T))`.

- [ ] **Step 4: Run manager tests on both targets and commit**

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net48 --filter FullyQualifiedName~DialogManagerTests --no-restore
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~DialogManagerTests --no-restore
```

Commit:

```powershell
git add Interface/Managers/IDialogManager.cs Common/Managers/DialogManager.cs Tests/SimpleNavigation.Tests/DialogManagerTests.cs
git commit -m "refactor: manage dialog windows by type"
```

## Task 3: Rebuild IDialogService and DialogService

**Files:**

- Modify: `Interface/Services/IDialogService.cs`
- Modify: `Services/DialogService.cs`
- Modify: `Tests/SimpleNavigation.Tests/TestTypes.cs`
- Create: `Tests/SimpleNavigation.Tests/DialogServiceTests.cs`

- [ ] **Step 1: Add dialog-aware and cancellation fixtures**

Add these test types:

```csharp
public sealed class AwareDialogViewModel : IDialogAware
{
    public int CallCount { get; private set; }
    public DialogParameters? Parameters { get; private set; }
    public Action<DialogParameters?>? RequestClose { get; set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        CallCount++;
        Parameters = parameters;
    }
}

public sealed class AwareWindow : Window, IDialogAware
{
    public int CallCount { get; private set; }
    public DialogParameters? Parameters { get; private set; }
    public Action<DialogParameters?>? RequestClose { get; set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        CallCount++;
        Parameters = parameters;
    }
}

public sealed class CancelClosingWindow : Window
{
    public bool CancelClose { get; set; } = true;

    public CancelClosingWindow()
    {
        Closing += (_, args) => args.Cancel = CancelClose;
    }
}
```

- [ ] **Step 2: Write failing service tests**

Create `DialogServiceTests.cs` with focused tests for:

```csharp
[Fact]
public void KeyShowResolvesTheDialogRouteThroughOrdinaryDI()
{
    StaTest.Run(() =>
    {
        var expected = new FirstWindow();
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        services.AddSingleton(expected);
        services.AddWindow<FirstWindow>("first");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDialogService>().Show("first");
        Assert.True(expected.IsVisible);
        expected.Close();
    });
}

[Fact]
public void CloseBeforeShowReturnsFalseWithoutResolvingAWindow()
{
    StaTest.Run(() =>
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        services.AddWindow<FirstWindow>("first");
        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IDialogService>().Close("first"));
    });
}

[Fact]
public void UnknownDialogKeyThrowsKeyNotFoundExceptionForShowAndClose()
{
    StaTest.Run(() =>
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IDialogService>();

        Assert.Throws<KeyNotFoundException>(() => service.Show("missing"));
        Assert.Throws<KeyNotFoundException>(() => service.Close("missing"));
    });
}
```

Add these concrete tests in the same class:

```csharp
[Fact]
public void GenericAndTypeShowDoNotRequireRoutes()
{
    StaTest.Run(() =>
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        services.AddTransient<FirstWindow>();
        services.AddTransient<SecondWindow>();
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IDialogService>();

        service.Show<FirstWindow>();
        Assert.True(provider.GetRequiredService<IDialogManager>()
            .GetExistingWindow(typeof(FirstWindow))!.IsVisible);
        Assert.True(service.Close<FirstWindow>());

        service.Show(typeof(SecondWindow));
        Assert.True(provider.GetRequiredService<IDialogManager>()
            .GetExistingWindow(typeof(SecondWindow))!.IsVisible);
        Assert.True(service.Close(typeof(SecondWindow)));
    });
}

[Fact]
public void WindowAndDistinctDataContextReceiveParametersOnceEach()
{
    StaTest.Run(() =>
    {
        var viewModel = new AwareDialogViewModel();
        var window = new AwareWindow { DataContext = viewModel };
        var parameters = new DialogParameters("id", 7);
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        services.AddSingleton(window);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDialogService>()
            .Show<AwareWindow>(parameters);

        Assert.Equal(1, window.CallCount);
        Assert.Equal(1, viewModel.CallCount);
        Assert.Same(parameters, window.Parameters);
        Assert.Same(parameters, viewModel.Parameters);
        window.Close();
    });
}

[Fact]
public void CancelledCloseReturnsFalseAndKeepsTheWindowManaged()
{
    StaTest.Run(() =>
    {
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        services.AddTransient<CancelClosingWindow>();
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IDialogService>();

        service.Show<CancelClosingWindow>();
        Assert.False(service.Close<CancelClosingWindow>());
        var window = Assert.IsType<CancelClosingWindow>(
            provider.GetRequiredService<IDialogManager>()
                .GetExistingWindow(typeof(CancelClosingWindow)));
        window.CancelClose = false;
        window.Close();
    });
}
```

Also add one cross-thread test that creates and shows the Window on an STA thread, then calls `Close` from the test thread and asserts `InvalidOperationException`. Never leave a shown Window open at the end of a test; set `CancelClose = false` before closing cancellation fixtures during cleanup.

Add this modal result regression test:

```csharp
[Fact]
public void ShowDialogReturnsParametersFromRequestClose()
{
    StaTest.Run(() =>
    {
        var viewModel = new AwareDialogViewModel();
        var window = new AwareWindow { DataContext = viewModel };
        var expected = new DialogParameters("saved", true);
        var services = new ServiceCollection();
        services.RegisterNavigationService();
        services.AddSingleton(window);
        using var provider = services.BuildServiceProvider();

        window.Dispatcher.BeginInvoke(new Action(() =>
            viewModel.RequestClose!.Invoke(expected)));

        var actual = provider.GetRequiredService<IDialogService>()
            .ShowDialog<AwareWindow>();

        Assert.Same(expected, actual);
        Assert.Null(viewModel.RequestClose);
        Assert.Null(window.RequestClose);
    });
}
```

Add a second modal test that schedules `window.Close()` instead of `RequestClose` and asserts a null result. Both tests must fail against the current unconditional `Closing` cancellation implementation.

- [ ] **Step 3: Run focused tests and verify RED**

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~DialogServiceTests --no-restore
```

Expected failures include missing overloads and the current modal close/result re-entry behavior.

- [ ] **Step 4: Implement the public service contract**

Replace `IDialogService` with the approved generic/Type/key overloads for `Show`, `ShowDialog`, and `Close`. Keep the existing namespace `SimpleNavigation.Interface.Services`.

- [ ] **Step 5: Implement resolution and validation helpers**

`DialogService` should inject `IServiceProvider`, `IDialogManager`, and `NavigationRouteRegistry` resolved from DI. Implement:

```csharp
private static Type ValidateWindowType(Type targetType)
{
    if (targetType == null)
        throw new ArgumentNullException(nameof(targetType));
    if (!typeof(Window).IsAssignableFrom(targetType))
        throw new ArgumentException(
            $"Target type '{targetType.FullName}' must derive from Window.",
            nameof(targetType));
    return targetType;
}

private Type ResolveDialogKey(string key) =>
    routes.GetRequiredDialogType(key);

private Window GetOrCreateWindow(Type targetType) =>
    dialogManager.GetOrCreateWindow(ValidateWindowType(targetType));
```

Generic and Type operations use ordinary DI through `DialogManager`; key operations resolve the route type first. A non-Window Type throws `ArgumentException`, null Type throws `ArgumentNullException`, and missing DI registrations propagate.

- [ ] **Step 6: Implement awareness and non-modal Show**

Create one helper that notifies Window first, then a distinct DataContext. Set `RequestClose` on both distinct awareness objects to close the same Window. For an already visible Window, call `Activate()` without calling `Show()` again. For a hidden but existing Window, call `Show()` then `Activate()`.

- [ ] **Step 7: Implement modal ShowDialog and Close**

For `ShowDialog`, attach close delegates that store the result then call `Close()`, allow the `Closing` event to proceed, call `ShowDialog()`, and clear both delegates in `finally`.

For `Close`, resolve the type/key, call `GetExistingWindow`, return false if null, call `Dispatcher.VerifyAccess`, subscribe a one-shot `Closed` observer, call `Close()`, and return whether the instance actually closed. Do not use `Window.IsActive` as a prerequisite. Detach the one-shot handler in all paths.

- [ ] **Step 8: Run service tests on both targets and commit Task 3**

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net48 --filter FullyQualifiedName~DialogServiceTests --no-restore
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~DialogServiceTests --no-restore
```

Then run the Dialog manager, extension, Page, Content, and Region tests. Commit only the Dialog interface/service/manager and associated tests:

```powershell
git add Interface/Services/IDialogService.cs Services/DialogService.cs Interface/Managers/IDialogManager.cs Common/Managers/DialogManager.cs Common/NavigationRouteRegistry.cs Extensions/NavigationExtensions.cs Tests/SimpleNavigation.Tests/DialogServiceTests.cs Tests/SimpleNavigation.Tests/DialogManagerTests.cs Tests/SimpleNavigation.Tests/NavigationExtensionsTests.cs Tests/SimpleNavigation.Tests/TestTypes.cs
git commit -m "feat: rebuild dialog service around keyed windows"
```

## Task 4: Update README and complete verification

**Files:**

- Modify: `README.md`

- [ ] **Step 1: Document Window registration and navigation**

Add examples:

```csharp
services.AddWindow<LoginWindow>("login");
services.AddWindow<SettingsWindow, SettingsViewModel>();
services.AddWindow<ReportsWindow, ReportsViewModel>("reports");

dialogService.Show<LoginWindow>();
dialogService.Show("reports");
dialogService.ShowDialog("login");
dialogService.Close("reports");
```

Explain that keys are independent from Page and Content keys, Window registrations are transient, instances are reused while open, and a fresh instance is created after `Closed`.

Remove the old warning that `ShowDialog` is broken after the repaired tests pass. Document that `Close` returns false when no instance is currently managed and that unknown keys throw.

- [ ] **Step 2: Verify README names and migration guidance**

Run:

```powershell
rg -n "AddWindow|ShowDialog|Close\(|Dialog key|transient|RegionService" README.md
git diff --check
```

Ensure the existing Region migration text and current user-owned changes are untouched.

- [ ] **Step 3: Run final verification without reverting baseline user changes**

First run the Dialog-focused suites on both targets and record results. Then run the full suite with the stable SDK. If the five baseline failures remain, report them separately and do not change unrelated Region/Page tests unless the user explicitly requests baseline repair.

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" restore SimpleNavigation.sln --nologo
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" build SimpleNavigation.sln -c Debug --no-restore --nologo
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net48 --no-build --nologo
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --no-build --nologo
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" build SimpleNavigation.sln -c Release --no-restore --nologo
```

- [ ] **Step 4: Audit the final diff**

Run:

```powershell
rg -n "RegionService" Common Extensions Interface Services SimpleNavigation.csproj
git diff --check
git status --short --branch
```

Do not stage or commit `Services/Region.cs`, `SimpleNavigation.csproj`, or any untracked user file. The final report must distinguish Dialog tests from the pre-existing five baseline failures if they remain.
