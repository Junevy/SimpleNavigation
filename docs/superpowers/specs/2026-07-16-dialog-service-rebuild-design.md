# DialogService Rebuild Design

## Goal

Rebuild `IDialogService` so Window registration and resolution follow the same ordinary-DI plus explicit-route model used by Page and Content navigation. Add generic, `Type`, and string-key operations for non-modal display, modal display, and closing an existing Window.

## Scope

This change will:

- Add Window and optional ViewModel registration extensions.
- Add an independent, ordinal, case-sensitive Dialog route namespace.
- Add generic, `Type`, and key overloads to `IDialogService`.
- Make `Close` operate only on existing managed Window instances and return a success result.
- Rework `DialogManager` to safely create, reuse, remove, and recreate transient Window instances.
- Fix the existing `ShowDialog` close/result re-entry bug.
- Preserve application ownership of `DataContext` and DI lifetimes.

This change will not:

- Set or replace a Window's `DataContext`.
- Use Microsoft DI keyed services.
- Create or dispose DI scopes for Window lifetimes.
- Automatically marshal Window operations across Dispatcher threads.

## Registration API

Add three `IServiceCollection` extensions:

```csharp
AddWindow<TWindow>(string key)
AddWindow<TWindow, TViewModel>()
AddWindow<TWindow, TViewModel>(string key)
```

Constraints:

```csharp
where TWindow : Window
where TViewModel : class
```

All extensions use `TryAddTransient`. Earlier application registrations are preserved, and the extensions never assign `DataContext`.

The single-generic overload registers the Window and its key. The double-generic overload without a key registers the Window and ViewModel only. The double-generic keyed overload registers both services and the Window route.

## Route Registry

Extend `NavigationRouteKind` with `Dialog` and add a third immutable route map to `NavigationRouteRegistry`.

```csharp
Type GetRequiredDialogType(string key);
```

Page, Content, and Dialog key spaces are independent. Each space uses `StringComparer.Ordinal`, so keys are case-sensitive. A duplicate key within Dialog registration throws `ArgumentException`, while the same key may exist in Page, Content, and Dialog maps.

String operations resolve in two stages:

```text
key -> Dialog route Type -> ordinary IServiceProvider.GetRequiredService(Type)
```

DI keyed services are not used.

## IDialogService Contract

The rebuilt interface is:

```csharp
public interface IDialogService
{
    void Show<TWindow>(DialogParameters? parameters = null)
        where TWindow : Window;

    void Show(Type targetType, DialogParameters? parameters = null);

    void Show(string key, DialogParameters? parameters = null);

    DialogParameters? ShowDialog<TWindow>(
        DialogParameters? parameters = null)
        where TWindow : Window;

    DialogParameters? ShowDialog(
        Type targetType,
        DialogParameters? parameters = null);

    DialogParameters? ShowDialog(
        string key,
        DialogParameters? parameters = null);

    bool Close<TWindow>() where TWindow : Window;

    bool Close(Type targetType);

    bool Close(string key);
}
```

Generic and `Type` operations require only ordinary DI registration. String operations require an explicit Dialog route and then resolve the mapped type through ordinary DI.

## DialogManager Responsibilities

`DialogManager` manages the current Window instance by Window `Type`, not by route key. Two keys mapped to the same Window type therefore address the same current instance.

It exposes capabilities equivalent to:

- Get or create a Window of a validated type through ordinary DI.
- Get an existing managed Window without creating one.
- Remove a Window when that exact instance raises `Closed`.

The instance map holds weak references. Removal checks both the type and exact Window reference so a delayed `Closed` notification from an old instance cannot remove a replacement instance.

### Window lifetime

1. The first `Show` or `ShowDialog` resolves a transient Window through DI, stores a weak reference, and subscribes to `Closed`.
2. Repeated `Show` while the Window is open reuses the same instance. It sends the new navigation parameters, calls `Show()` only when the Window is not visible, and then calls `Activate()`.
3. When the Window closes, `DialogManager` removes the exact cached instance.
4. A later display resolves a new transient Window through DI.
5. `Close` never creates a Window.

## Show And ShowDialog Behavior

Before presentation, the service validates the target type and obtains the Window from `DialogManager`.

Navigation awareness is delivered in this order:

1. Window when it implements `IDialogAware`.
2. Window `DataContext` when it implements `IDialogAware` and is not the same reference as the Window.

Both receive `OnNavigated(parameters)`. The service never assigns `DataContext`.

For non-modal `Show`, `RequestClose` closes the current Window. Repeated calls update awareness and close delegates for the current instance.

For modal `ShowDialog`:

- `RequestClose(result)` stores the result and calls `Window.Close()`.
- The current unconditional `Closing` cancellation and recursive close path are removed.
- Closing through the system close button is allowed and returns `null` when no result was supplied.
- Both Window and distinct DataContext close delegates are cleared when modal display ends.
- Window, DI, Dispatcher, and awareness exceptions propagate to the caller.

## Close Behavior

All close overloads resolve a Window type, then query `DialogManager` for an existing instance without using DI.

- Unknown string key throws `KeyNotFoundException`.
- A registered type with no current Window returns `false`.
- A Window that exists but is not focused may still be closed; WPF `IsActive` is not a prerequisite.
- `Close()` is called on the Window's Dispatcher thread after `VerifyAccess()`.
- If `Closing` cancels the operation, the method returns `false`.
- If the Window raises `Closed` and is removed from the manager, the method returns `true`.

## Validation And Error Handling

- Null `Type` throws `ArgumentNullException`.
- A non-Window `Type` throws `ArgumentException`.
- A null, empty, or whitespace route key throws `ArgumentException`.
- An unknown Dialog key throws `KeyNotFoundException`.
- Missing DI registration preserves the original `GetRequiredService` exception.
- Window operations require the Window Dispatcher thread; the library does not marshal automatically.
- `IDialogAware` exceptions propagate.

## Testing Strategy

Tests target both `net48` and `net8.0-windows` and cover:

- All three `AddWindow` overloads and transient registration.
- Preservation of earlier application registrations.
- Independent Page, Content, and Dialog route namespaces.
- Ordinal key casing, duplicate Dialog keys, invalid keys, and unknown keys.
- Generic, `Type`, and key `Show` paths.
- Generic, `Type`, and key `ShowDialog` paths and result propagation.
- Generic, `Type`, and key `Close` paths.
- Close-before-show returning `false` without creating a Window.
- Closing cancellation returning `false`.
- Reuse while open and recreation after `Closed`.
- Window and DataContext awareness order, de-duplication, and exception propagation.
- Dispatcher enforcement and DI exception preservation.
- Regression coverage for Page, Content, Region, and registration behavior.

## Documentation And Migration

README examples will show the three Window registration extensions, generic/`Type`/key display operations, key-based close, and transient recreation after close. The existing warning about broken modal closure will be removed only after the repaired behavior is verified by tests.
