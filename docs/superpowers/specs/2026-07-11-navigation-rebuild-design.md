# SimpleNavigation Rebuild Design

## Status

This design was approved section by section on 2026-07-11. All implementation work is based on the `rebuild` branch.

## Summary

The rebuild separates region ownership from navigation services, replaces the public `RegionService` attached-property owner with `Region`, adds navigation for non-page WPF content, and adds explicit string route aliases without replacing the existing generic and `Type` navigation paths.

The library remains DI-driven and continues to target `net48` and `net8.0-windows`. The rebuild is intentionally breaking: `RegionService` is deleted rather than retained as a compatibility facade.

## Goals

- Move region registration, removal, and lookup out of `PageService` into `IRegionManager` and `RegionManager`.
- Rename the XAML attached-property owner from `RegionService` to `Region` and support `Frame` plus non-`Frame` `ContentControl` hosts.
- Add `IContentService` and `ContentService` for navigating to `FrameworkElement` content other than `Page` and `Window`.
- Add string key navigation to both page and content services through explicit route registration.
- Add a shared `INavigationAware` callback contract for navigated views and view models.
- Preserve generic and `Type` navigation as direct DI resolution paths.
- Keep host behavior extensible through internal adapters so `Panel` and `TabControl` can be added later without rewriting navigation services.
- Add automated tests for both target frameworks and update the README for the rebuilt API.

## Non-goals

- Do not implement `Grid`, `StackPanel`, `Panel`, or `TabControl` hosts in this change.
- Do not expose a public adapter plug-in API before those host semantics are defined.
- Do not set or replace a view's `DataContext`.
- Do not infer routes from type names, attributes, reflection scanning, or DI keyed services.
- Do not add back navigation to ordinary `ContentControl` regions.
- Do not add asynchronous navigation, navigation scopes, view caching, or automatic cross-thread dispatch.

## Breaking Changes

- Delete `SimpleNavigation.Services.RegionService` and its `RegionRegisted` event.
- Add `SimpleNavigation.Services.Region` as the only attached-property owner. XAML consumers must change `RegionService.RegionName` to `Region.RegionName`.
- Remove `GetRegion` from `IPageService`.
- Remove `RegisterRegion` and the region dictionary from `PageService`.
- Add correctly spelled `GoBack`. Retain `Goback` only as an obsolete forwarding member to avoid an unrelated hard break.
- Change page navigation awareness so the navigated view and its `DataContext` can both receive the shared callback.
- Change invalid navigation configuration from silent no-op behavior to explicit exceptions.

## Architecture

### Region declaration

`Region` is a static attached-property owner. It declares `RegionNameProperty` and the standard `GetRegionName` and `SetRegionName` accessors. It contains no page or content navigation logic.

An internal host adapter resolver validates each declared host. The resolver checks `Frame` before `ContentControl` because `Frame` inherits `ContentControl`. The built-in host set is:

- `Frame`, owned by `PageService` and the frame adapter.
- Non-`Frame` `ContentControl`, owned by `ContentService` and the content-control adapter.

The static declaration layer maintains weak registration records and publishes internal registration changes. A newly created `RegionManager` subscribes to changes before importing the current weak snapshot; registering the same name and host twice is idempotent. This ordering prevents both a snapshot-to-subscription race and regions declared during XAML initialization from being lost when DI creates the manager later.

Changing a region name removes the old registration before adding the new registration. Clearing the property or unloading the host unregisters its attached-property ownership. Loading it again restores that ownership with a new activation token, so a delayed notification from an older load cycle cannot remove the new registration. Static declarations use weak references so an abandoned visual tree is not retained by the attached-property infrastructure.

### Region manager

`IRegionManager` is the public region ownership boundary:

```csharp
public interface IRegionManager
{
    void RegisterRegion(string regionName, FrameworkElement region);

    bool UnregisterRegion(string regionName, FrameworkElement region);

    FrameworkElement? GetRegion(string regionName);

    TRegion? GetRegion<TRegion>(string regionName)
        where TRegion : FrameworkElement;
}
```

`RegionManager` is registered as a singleton by `RegisterNavigationService`. It owns named lookup, imports attached-property declarations, and unsubscribes from static declaration notifications when disposed. The element argument on `UnregisterRegion` prevents a stale unload notification from removing a newer host that reused the same name.

Both attached-property and programmatic registration pass through the same adapter resolver. Programmatic callers cannot register an unsupported host type to bypass `Region` validation. Named manager entries hold weak host references and remove dead entries during lookup or replacement.

Programmatic and attached-property ownership are tracked independently for the same name and host. Public `RegisterRegion` and `UnregisterRegion` add or remove only programmatic ownership. Internal declaration notifications add or remove a specific activation token. A region remains available while either ownership source is active. This prevents `Unloaded` from accidentally removing a programmatically registered region and prevents a stale unload token from removing a reloaded declaration.

Only one live host may own a region name. A second live host with the same name is a configuration error. A dead weak registration may be cleaned up and replaced.

### Host adapters

Adapters remain internal in this release:

- `FrameRegionAdapter` verifies dispatcher access, calls `Frame.Navigate`, and implements journal back navigation.
- `ContentControlRegionAdapter` verifies dispatcher access and assigns `ContentControl.Content`. It explicitly refuses `Frame` hosts and has no journal capability.

The adapter resolver is the only component that distinguishes concrete host types. Adding a future `PanelRegionAdapter` or `TabControlRegionAdapter` will extend this resolver and define the new host's presentation semantics without changing `PageService`, `ContentService`, or `RegionManager`.

`Panel` support must eventually define whether the region owns all children before using `Children.Clear()` and `Children.Add()`. `TabControl` support must define whether navigation creates a tab, replaces the selected tab, or reuses an existing item, including how headers are supplied. Those policies are intentionally deferred.

### Route registry

An internal route registry holds two independent, ordinal, case-sensitive maps:

- Page key to a type assignable to `Page`.
- Content key to a type assignable to `FrameworkElement`, excluding `Page` and `Window`.

The same key may exist once in each map. A key may not be null, empty, or whitespace. Registering the same key twice in one map throws during service collection configuration, including when both registrations point to the same type.

The route registry stores only aliases. It does not create instances and does not use Microsoft DI keyed services. This keeps behavior identical for the DI 6 dependency used by `net48` and the DI 8 dependency used by `net8.0-windows`.

## Public Navigation APIs

### Page navigation

```csharp
public interface IPageService
{
    void Navigate<TPage>(
        string regionName,
        DialogParameters? parameters = null)
        where TPage : Page;

    void Navigate(
        string regionName,
        Type targetType,
        DialogParameters? parameters = null);

    void Navigate(
        string regionName,
        string key,
        DialogParameters? parameters = null);

    void GoBack(string regionName);

    [Obsolete("Use GoBack instead.")]
    void Goback(string regionName);
}
```

`PageService` accepts only `Frame` regions and only `Page` targets.

### Content navigation

```csharp
public interface IContentService
{
    void Navigate<TContent>(
        string regionName,
        DialogParameters? parameters = null)
        where TContent : FrameworkElement;

    void Navigate(
        string regionName,
        Type targetType,
        DialogParameters? parameters = null);

    void Navigate(
        string regionName,
        string key,
        DialogParameters? parameters = null);
}
```

`ContentService` accepts only non-`Frame` `ContentControl` regions. A content target may be a `UserControl`, ordinary `ContentControl`, custom control, `Grid`, `StackPanel`, or another `FrameworkElement`; `Page` and `Window` targets are rejected. A `Frame` may be content, but a `Frame` may not be the region host used by `ContentService`.

### Navigation awareness

```csharp
public interface INavigationAware
{
    void OnNavigated(DialogParameters? parameters);
}
```

`IPageAware` inherits `INavigationAware` and retains its existing `Receive` event. A successful navigation synchronously checks both the target view and its `DataContext`. Every distinct object that implements `INavigationAware` is invoked once. If the view and `DataContext` reference the same instance, it is invoked only once. The callback is invoked even when `parameters` is null.

The library never assigns `DataContext`. Constructor injection, factories, XAML, and view-to-view-model wiring remain application responsibilities.

## Service Collection Extensions

`NavigationExtensions` adds the following overloads:

```csharp
IServiceCollection AddPage<TPage>(string key)
    where TPage : Page;

IServiceCollection AddPage<TPage, TViewModel>()
    where TPage : Page
    where TViewModel : class;

IServiceCollection AddPage<TPage, TViewModel>(string key)
    where TPage : Page
    where TViewModel : class;

IServiceCollection AddContent<TView>(string key)
    where TView : FrameworkElement;

IServiceCollection AddContent<TView, TViewModel>()
    where TView : FrameworkElement
    where TViewModel : class;

IServiceCollection AddContent<TView, TViewModel>(string key)
    where TView : FrameworkElement
    where TViewModel : class;
```

Registration behavior is:

- Single-generic key overload: `TryAddTransient` the view and add its route alias.
- Double-generic overload without key: `TryAddTransient` the view and view model, with no route alias.
- Double-generic key overload: `TryAddTransient` the view and view model, then add the route alias.
- `AddContent` performs a runtime guard that rejects `Page` and `Window`, which cannot be expressed as a negative generic constraint.
- Existing application registrations are not replaced. To select a custom lifetime or factory, register it before calling the navigation extension.
- The extension does not set `DataContext` and does not infer view-model interfaces.

`AddPage` and `AddContent` may be called before or after `RegisterNavigationService` as long as all calls occur before `BuildServiceProvider`.

## Navigation Data Flow

The three overload families deliberately have separate resolution paths.

### Generic navigation

```csharp
var target = provider.GetRequiredService<TTarget>();
```

Generic navigation never reads the route registry.

### Type navigation

```csharp
var target = provider.GetRequiredService(targetType) as TExpectedBase;
```

The service validates assignability before resolving. Type navigation never reads the route registry.

### String key navigation

```csharp
var targetType = routes.GetRequiredType(key);
var target = provider.GetRequiredService(targetType) as TExpectedBase;
```

String navigation first resolves the alias to a type in the appropriate page or content map, then uses the ordinary DI container to create the instance. It does not call `GetRequiredKeyedService`.

After target resolution, every navigation follows this order:

1. Validate the region name and target input.
2. Resolve the target instance through the selected path above.
3. Retrieve the named region and require the correct host adapter.
4. Verify access to the host dispatcher.
5. Ask the adapter to present or navigate to the target.
6. If the host accepted navigation, invoke navigation awareness on the view and its `DataContext`.

For `Frame`, the awareness callback runs synchronously after `Frame.Navigate` returns `true`; it does not wait for `Loaded`, `Navigated`, or `LoadCompleted`. For `ContentControl`, it runs after the `Content` assignment succeeds.

`GoBack` retrieves the named `Frame`, verifies dispatcher access, and calls `GoBack` only when `CanGoBack` is true. A valid frame with no journal entry is a no-op. `Goback` forwards to `GoBack`.

## Error Handling

- Null, empty, or whitespace region names and route keys throw `ArgumentException`.
- An unregistered key throws `KeyNotFoundException` and names the key plus the page or content route category.
- A duplicate key in one route category throws `ArgumentException` while configuring `IServiceCollection`.
- A missing region or a region with the wrong host type throws `InvalidOperationException` and names the region, actual type when available, and expected host type.
- A non-`Page` target passed to `PageService` throws `ArgumentException`.
- A `Page` or `Window` target passed to `ContentService` throws `ArgumentException`.
- A missing DI registration retains the original `GetRequiredService` exception.
- A resolved object that does not match the validated expected base type throws `InvalidOperationException` rather than producing a null navigation target.
- Host, dispatcher, content assignment, navigation, and `INavigationAware` exceptions propagate to the caller.
- If `Frame.Navigate` returns `false`, awareness callbacks are not invoked.
- A second live region with an existing name throws `InvalidOperationException`.
- Attaching `RegionName` to, or programmatically registering, a host without a built-in adapter throws `ArgumentException` and names the unsupported type.
- `GetRegion` returns null when a name is absent; navigation services convert absence into the fail-fast exception above.
- `UnregisterRegion` returns false when the name is absent or belongs to a different element.

## Threading And Lifetime

Navigation APIs are synchronous and must be called on the region host's dispatcher thread. Adapters call `VerifyAccess`; the library does not marshal work to another dispatcher.

`IRegionManager`, `IPageService`, `IContentService`, and the internal route registry are DI singletons. Views and view models added by navigation extensions default to transient through `TryAddTransient`. Existing application registrations control their actual lifetime.

Region declarations and manager lookups use weak references where ownership is not required. Active WPF hosts unregister on unload, and `RegionManager` releases static subscriptions when disposed.

## Planned File Structure

- Delete `Services/RegionService.cs`.
- Create `Services/Region.cs` for the attached property and weak declaration notifications.
- Create `Interface/IRegionManager.cs` and `Common/RegionManager.cs` for region ownership.
- Create focused internal adapter files for `Frame` and `ContentControl` host behavior.
- Create `Interface/IContentService.cs` and `Services/ContentService.cs`.
- Create `Interface/INavigationAware.cs` and update `Interface/IPageAware.cs`.
- Update `Interface/IPageService.cs` and `Services/PageService.cs`.
- Add an internal page/content route registry and route descriptors.
- Update `Extensions/NavigationExtensions.cs` with core service registration and the six route/view overloads.
- Add `Tests/SimpleNavigation.Tests` to the solution.
- Update `README.md` with the rebuilt API, examples, limitations, and migration note.

## Testing Strategy

Tests use xUnit and an STA helper for WPF behavior. The test project targets `net48` and `net8.0-windows`.

Coverage includes:

- `Region` declaration on `Frame` and ordinary `ContentControl`.
- Rename, clear, unload, reload, weak cleanup, late `RegionManager` creation, and duplicate region detection.
- Programmatic register, unregister, unregistration ownership, untyped lookup, and typed lookup.
- Mixed programmatic and attached ownership, including unload/reload activation-token ordering.
- Rejection of unsupported hosts through both attached-property and programmatic registration paths.
- All six `AddPage` and `AddContent` overloads.
- Default transient registration, preservation of an existing singleton, duplicate keys, separate page/content key spaces, and key validation.
- Generic, `Type`, and string navigation for pages and content.
- Proof that generic and `Type` paths do not require a route alias.
- Proof that string paths resolve the dictionary type and then use the ordinary DI registration.
- Content host replacement and frame journal navigation.
- `GoBack`, no-journal behavior, and obsolete `Goback` forwarding.
- Rejection of page targets, window targets, frame content hosts, wrong region hosts, missing regions, invalid types, missing DI registrations, and unknown keys.
- View and `DataContext` awareness, reference de-duplication, null parameters, callback ordering, rejected navigation, and callback exception propagation.
- Dispatcher-thread enforcement.
- Successful Debug and Release builds for both library target frameworks.

Each production behavior is implemented with a red-green-refactor cycle. The final verification runs the complete test suite and builds the solution for both targets.

## Documentation

The README will:

- Replace every `RegionService.RegionName` example with `Region.RegionName`.
- Document the removal of `RegionService` as a breaking migration.
- Document `IRegionManager`, `IContentService`, and `INavigationAware`.
- Show the six `AddPage` and `AddContent` registration forms.
- Show generic, `Type`, and string key navigation.
- Explain that route aliases map strings to types while DI remains responsible for instances.
- State that the library never assigns `DataContext`.
- State that content regions currently require a non-`Frame` `ContentControl`.
- Explain how the internal adapter boundary permits future `Panel` and `TabControl` support without claiming those hosts work today.
