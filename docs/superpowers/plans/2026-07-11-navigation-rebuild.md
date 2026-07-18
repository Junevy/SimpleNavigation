# Navigation Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild SimpleNavigation around `RegionManager`, add non-page content navigation and explicit string routes, and remove `RegionService` while preserving DI-driven instance creation on `net48` and `net8.0-windows`.

**Architecture:** `Region` declares supported WPF hosts, `RegionManager` owns weak named lookup, and internal host adapters isolate `Frame` from ordinary `ContentControl` behavior. `PageService` and `ContentService` resolve views through ordinary Microsoft DI; only string overloads consult separate page/content route maps before resolving the mapped type.

**Tech Stack:** C# 13, WPF, Microsoft.Extensions.DependencyInjection 6/8, xUnit 2.5.3, Microsoft.NET.Test.Sdk 17.11.1, .NET Framework 4.8, .NET 8 Windows.

---

## File Map

- `SimpleNavigation.csproj`: exclude the nested test tree from the root SDK project's default items.
- `SimpleNavigation.sln`: include the new test project.
- `Tests/SimpleNavigation.Tests/SimpleNavigation.Tests.csproj`: dual-target WPF test project.
- `Tests/SimpleNavigation.Tests/TestInfrastructure/StaTest.cs`: execute WPF assertions on an STA thread and pump the dispatcher.
- `Tests/SimpleNavigation.Tests/TestInfrastructure/AssemblyInfo.cs`: disable test parallelism because region declarations are process-static.
- `Tests/SimpleNavigation.Tests/TestTypes.cs`: focused page, view, view-model, and awareness fixtures.
- `Tests/SimpleNavigation.Tests/RegionManagerTests.cs`: programmatic region ownership tests.
- `Tests/SimpleNavigation.Tests/RegionTests.cs`: attached-property and lifecycle tests.
- `Tests/SimpleNavigation.Tests/NavigationExtensionsTests.cs`: DI and route registration tests.
- `Tests/SimpleNavigation.Tests/PageServiceTests.cs`: page navigation, awareness, error, and journal tests.
- `Tests/SimpleNavigation.Tests/ContentServiceTests.cs`: content navigation, awareness, and host-boundary tests.
- `Interface/IRegionManager.cs`: public region registry contract.
- `Common/RegionManager.cs`: weak region lookup and attached-declaration synchronization.
- `Common/RegionHostAdapter.cs`: internal host kind, base adapter contract, and ordered resolver.
- `Common/FrameRegionAdapter.cs`: `Frame.Navigate` and journal behavior.
- `Common/ContentControlRegionAdapter.cs`: non-`Frame` content replacement behavior.
- `Services/Region.cs`: attached property plus weak declaration catalog.
- `Common/NavigationRouteRegistry.cs`: immutable page/content key maps.
- `Common/NavigationAwareNotifier.cs`: view and DataContext callback de-duplication.
- `Interface/INavigationAware.cs`: shared navigation callback.
- `Interface/IContentService.cs`: public content navigation contract.
- `Services/ContentService.cs`: DI-driven content navigation.
- `Interface/IPageAware.cs`: inherit the shared awareness contract.
- `Interface/IPageService.cs`: add key navigation and `GoBack`, remove region lookup.
- `Services/PageService.cs`: delegate region ownership and support all three resolution paths.
- `Extensions/NavigationExtensions.cs`: register core services and the six `AddPage`/`AddContent` overloads.
- `README.md`: rebuilt API, examples, migration note, and future-host boundary.
- Delete `Services/RegionService.cs` after `PageService` no longer references it.

Use this stable SDK invocation throughout because the machine's default .NET 10 RC SDK lacks its matching runtime:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" <command>
```

### Task 1: Add The Dual-Target WPF Test Harness

**Files:**
- Modify: `SimpleNavigation.csproj`
- Modify: `SimpleNavigation.sln`
- Create: `Tests/SimpleNavigation.Tests/SimpleNavigation.Tests.csproj`
- Create: `Tests/SimpleNavigation.Tests/TestInfrastructure/AssemblyInfo.cs`
- Create: `Tests/SimpleNavigation.Tests/TestInfrastructure/StaTest.cs`
- Create: `Tests/SimpleNavigation.Tests/SmokeTests.cs`

- [ ] **Step 1: Exclude tests from the root library's default SDK items**

Add this property to the main `PropertyGroup` in `SimpleNavigation.csproj`:

```xml
<DefaultItemExcludesInProjectFolder>$(DefaultItemExcludesInProjectFolder);Tests/**</DefaultItemExcludesInProjectFolder>
```

- [ ] **Step 2: Create the test project**

Create `Tests/SimpleNavigation.Tests/SimpleNavigation.Tests.csproj` with exactly:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>13</LangVersion>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\SimpleNavigation.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

Add it to the solution:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" sln SimpleNavigation.sln add Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj
```

- [ ] **Step 3: Add deterministic STA infrastructure**

Create `TestInfrastructure/AssemblyInfo.cs`:

```csharp
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Create `TestInfrastructure/StaTest.cs`:

```csharp
using System.Runtime.ExceptionServices;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace SimpleNavigation.Tests.TestInfrastructure
{
    internal static class StaTest
    {
        public static void Run(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("The STA test did not finish within 20 seconds.");

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        public static void PumpDispatcher()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        public static void PumpUntil(Func<bool> condition)
        {
            var timeout = Stopwatch.StartNew();
            while (!condition())
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(5))
                    throw new TimeoutException("The WPF condition was not reached within 5 seconds.");
                PumpDispatcher();
            }
        }
    }
}
```

- [ ] **Step 4: Add and run a baseline smoke test**

Create `SmokeTests.cs`:

```csharp
using SimpleNavigation.Common;

namespace SimpleNavigation.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void DialogParametersRoundTripsAValue()
        {
            var parameters = new DialogParameters("answer", 42);

            Assert.Equal(42, parameters.Get<int>("answer"));
        }
    }
}
```

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" restore SimpleNavigation.sln
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj --no-restore
```

Expected: both `net48` and `net8.0-windows` pass one test with zero failures.

- [ ] **Step 5: Commit the test harness**

```powershell
git add SimpleNavigation.csproj SimpleNavigation.sln Tests
git commit -m "test: add dual-target WPF test harness"
```

### Task 2: Move Programmatic Region Ownership Into RegionManager

**Files:**
- Create: `Interface/IRegionManager.cs`
- Create: `Common/RegionHostAdapter.cs`
- Create: `Common/FrameRegionAdapter.cs`
- Create: `Common/ContentControlRegionAdapter.cs`
- Create: `Common/RegionManager.cs`
- Create: `Tests/SimpleNavigation.Tests/RegionManagerTests.cs`

- [ ] **Step 1: Write failing public-contract tests**

Create `RegionManagerTests.cs`:

```csharp
using SimpleNavigation.Common;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace SimpleNavigation.Tests
{
    public class RegionManagerTests
    {
        [Fact]
        public void RegisterGetAndUnregisterPreserveHostIdentity()
        {
            StaTest.Run(() =>
            {
                var manager = new RegionManager();
                var host = new ContentControl();

                manager.RegisterRegion("main", host);

                Assert.Same(host, manager.GetRegion("main"));
                Assert.Same(host, manager.GetRegion<ContentControl>("main"));
                Assert.Null(manager.GetRegion<Frame>("main"));
                Assert.True(manager.UnregisterRegion("main", host));
                Assert.Null(manager.GetRegion("main"));
            });
        }

        [Fact]
        public void RegisteringTheSameHostTwiceIsIdempotent()
        {
            StaTest.Run(() =>
            {
                var manager = new RegionManager();
                var host = new Frame();

                manager.RegisterRegion("main", host);
                manager.RegisterRegion("main", host);

                Assert.Same(host, manager.GetRegion("main"));
            });
        }

        [Fact]
        public void ASecondLiveHostWithTheSameNameIsRejected()
        {
            StaTest.Run(() =>
            {
                var manager = new RegionManager();
                var first = new ContentControl();
                manager.RegisterRegion("main", first);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => manager.RegisterRegion("main", new ContentControl()));

                Assert.Contains("main", exception.Message);
                GC.KeepAlive(first);
            });
        }

        [Fact]
        public void UnregisterDoesNotRemoveAnotherHostsRegistration()
        {
            StaTest.Run(() =>
            {
                var manager = new RegionManager();
                var owner = new ContentControl();
                manager.RegisterRegion("main", owner);

                Assert.False(manager.UnregisterRegion("main", new ContentControl()));
                Assert.Same(owner, manager.GetRegion("main"));
            });
        }

        [Fact]
        public void UnsupportedProgrammaticHostIsRejected()
        {
            StaTest.Run(() =>
            {
                var manager = new RegionManager();
                var exception = Assert.Throws<ArgumentException>(
                    () => manager.RegisterRegion("main", new Grid()));

                Assert.Contains(typeof(Grid).FullName!, exception.Message);
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void InvalidRegionNameIsRejected(string regionName)
        {
            StaTest.Run(() =>
            {
                var manager = new RegionManager();
                Assert.Throws<ArgumentException>(
                    () => manager.RegisterRegion(regionName, new ContentControl()));
            });
        }

        [Fact]
        public void ManagerDoesNotKeepAnAbandonedHostAlive()
        {
            StaTest.Run(() =>
            {
                var manager = new RegionManager();
                var weakHost = RegisterTemporaryHost(manager);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Assert.False(weakHost.IsAlive);
                Assert.Null(manager.GetRegion("temporary"));
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RegisterTemporaryHost(RegionManager manager)
        {
            var host = new ContentControl();
            manager.RegisterRegion("temporary", host);
            return new WeakReference(host);
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~RegionManagerTests --no-restore
```

Expected: build fails because `RegionManager` and `IRegionManager` do not exist.

- [ ] **Step 3: Add the minimal region contract and host classification**

Create `Interface/IRegionManager.cs`:

```csharp
using System.Windows;

namespace SimpleNavigation.Interface
{
    public interface IRegionManager
    {
        void RegisterRegion(string regionName, FrameworkElement region);
        bool UnregisterRegion(string regionName, FrameworkElement region);
        FrameworkElement? GetRegion(string regionName);
        TRegion? GetRegion<TRegion>(string regionName) where TRegion : FrameworkElement;
    }
}
```

Create `Common/RegionHostAdapter.cs`:

```csharp
using System.Windows;

namespace SimpleNavigation.Common
{
    internal enum RegionHostKind
    {
        Page,
        Content
    }

    internal interface IRegionHostAdapter
    {
        RegionHostKind Kind { get; }
        bool CanHandle(FrameworkElement region);
    }

    internal static class RegionHostAdapterResolver
    {
        private static readonly IRegionHostAdapter[] Adapters =
        {
            new FrameRegionAdapter(),
            new ContentControlRegionAdapter()
        };

        public static IRegionHostAdapter GetRequired(FrameworkElement region)
        {
            foreach (var adapter in Adapters)
            {
                if (adapter.CanHandle(region))
                    return adapter;
            }

            throw new ArgumentException(
                $"Region host type '{region.GetType().FullName}' is not supported.",
                nameof(region));
        }
    }
}
```

Create `FrameRegionAdapter.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common
{
    internal sealed class FrameRegionAdapter : IRegionHostAdapter
    {
        public RegionHostKind Kind => RegionHostKind.Page;

        public bool CanHandle(FrameworkElement region) => region is Frame;
    }
}
```

Create `ContentControlRegionAdapter.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Common
{
    internal sealed class ContentControlRegionAdapter : IRegionHostAdapter
    {
        public RegionHostKind Kind => RegionHostKind.Content;

        public bool CanHandle(FrameworkElement region) =>
            region is ContentControl && region is not Frame;
    }
}
```

- [ ] **Step 4: Implement weak programmatic region lookup**

Create `Common/RegionManager.cs`:

```csharp
using SimpleNavigation.Interface;
using System.Windows;

namespace SimpleNavigation.Common
{
    public sealed class RegionManager : IRegionManager
    {
        private readonly Dictionary<string, RegionEntry> regions =
            new(StringComparer.Ordinal);
        private readonly object syncRoot = new();

        public void RegisterRegion(string regionName, FrameworkElement region)
        {
            ValidateName(regionName);
            if (region == null)
                throw new ArgumentNullException(nameof(region));
            RegionHostAdapterResolver.GetRequired(region);

            lock (syncRoot)
            {
                if (regions.TryGetValue(regionName, out var existing) &&
                    existing.Region.TryGetTarget(out var existingRegion))
                {
                    if (ReferenceEquals(existingRegion, region))
                    {
                        existing.IsProgrammatic = true;
                        return;
                    }

                    throw new InvalidOperationException(
                        $"Region '{regionName}' is already registered by '{existingRegion.GetType().FullName}'.");
                }

                regions[regionName] = new RegionEntry(region)
                {
                    IsProgrammatic = true
                };
            }
        }

        public bool UnregisterRegion(string regionName, FrameworkElement region)
        {
            ValidateName(regionName);
            if (region == null)
                throw new ArgumentNullException(nameof(region));

            lock (syncRoot)
            {
                if (!regions.TryGetValue(regionName, out var existing) ||
                    !existing.Region.TryGetTarget(out var existingRegion) ||
                    !ReferenceEquals(existingRegion, region) ||
                    !existing.IsProgrammatic)
                    return false;

                existing.IsProgrammatic = false;
                if (existing.DeclarationTokens.Count == 0)
                    regions.Remove(regionName);
                return true;
            }
        }

        public FrameworkElement? GetRegion(string regionName)
        {
            ValidateName(regionName);

            lock (syncRoot)
            {
                if (!regions.TryGetValue(regionName, out var entry))
                    return null;

                if (entry.Region.TryGetTarget(out var region))
                    return region;

                regions.Remove(regionName);
                return null;
            }
        }

        public TRegion? GetRegion<TRegion>(string regionName)
            where TRegion : FrameworkElement => GetRegion(regionName) as TRegion;

        private static void ValidateName(string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                throw new ArgumentException("Region name cannot be null or whitespace.", nameof(regionName));
        }

        private sealed class RegionEntry
        {
            public RegionEntry(FrameworkElement region)
            {
                Region = new WeakReference<FrameworkElement>(region);
            }

            public WeakReference<FrameworkElement> Region { get; }
            public bool IsProgrammatic { get; set; }
            public HashSet<long> DeclarationTokens { get; } = new();
        }
    }
}
```

- [ ] **Step 5: Run manager tests on both targets**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj --filter FullyQualifiedName~RegionManagerTests --no-restore
```

Expected: all `RegionManagerTests` pass for `net48` and `net8.0-windows`.

- [ ] **Step 6: Commit programmatic region ownership**

```powershell
git add Interface/IRegionManager.cs Common/RegionHostAdapter.cs Common/FrameRegionAdapter.cs Common/ContentControlRegionAdapter.cs Common/RegionManager.cs Tests/SimpleNavigation.Tests/RegionManagerTests.cs
git commit -m "feat: add region manager"
```

### Task 3: Replace Attached Region Registration With Region

**Files:**
- Create: `Services/Region.cs`
- Modify: `Common/RegionManager.cs`
- Create: `Tests/SimpleNavigation.Tests/RegionTests.cs`

- [ ] **Step 1: Write failing attached-property lifecycle tests**

Create `RegionTests.cs` with these tests and a cleanup helper:

```csharp
using SimpleNavigation.Common;
using SimpleNavigation.Services;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Tests
{
    public class RegionTests
    {
        [Fact]
        public void FrameDeclaredBeforeManagerCreationIsReplayed()
        {
            StaTest.Run(() =>
            {
                var frame = new Frame();
                try
                {
                    Region.SetRegionName(frame, "main");
                    using var manager = new RegionManager();

                    Assert.Same(frame, manager.GetRegion<Frame>("main"));
                }
                finally
                {
                    frame.ClearValue(Region.RegionNameProperty);
                }
            });
        }

        [Fact]
        public void ContentControlIsSupportedAndGridHostIsRejected()
        {
            StaTest.Run(() =>
            {
                var content = new ContentControl();
                try
                {
                    Region.SetRegionName(content, "content");
                    using var manager = new RegionManager();
                    Assert.Same(content, manager.GetRegion("content"));

                    Assert.Throws<ArgumentException>(
                        () => Region.SetRegionName(new Grid(), "grid"));
                }
                finally
                {
                    content.ClearValue(Region.RegionNameProperty);
                }
            });
        }

        [Fact]
        public void RenameUnregistersOldNameAndRegistersNewName()
        {
            StaTest.Run(() =>
            {
                using var manager = new RegionManager();
                var host = new ContentControl();
                try
                {
                    Region.SetRegionName(host, "old");
                    Region.SetRegionName(host, "new");

                    Assert.Null(manager.GetRegion("old"));
                    Assert.Same(host, manager.GetRegion("new"));
                }
                finally
                {
                    host.ClearValue(Region.RegionNameProperty);
                }
            });
        }

        [Fact]
        public void ClearingTheAttachedPropertyUnregistersTheHost()
        {
            StaTest.Run(() =>
            {
                using var manager = new RegionManager();
                var host = new ContentControl();
                Region.SetRegionName(host, "main");

                host.ClearValue(Region.RegionNameProperty);

                Assert.Null(manager.GetRegion("main"));
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AttachedPropertyRejectsInvalidNames(string name)
        {
            StaTest.Run(() =>
                Assert.Throws<ArgumentException>(
                    () => Region.SetRegionName(new ContentControl(), name)));
        }

        [Fact]
        public void UnloadRemovesAndLoadRestoresTheRegion()
        {
            StaTest.Run(() =>
            {
                using var manager = new RegionManager();
                var host = new ContentControl();
                try
                {
                    Region.SetRegionName(host, "main");
                    host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    Assert.Null(manager.GetRegion("main"));

                    host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Assert.Same(host, manager.GetRegion("main"));
                }
                finally
                {
                    host.ClearValue(Region.RegionNameProperty);
                }
            });
        }

        [Fact]
        public void AttachedUnloadDoesNotRemoveProgrammaticOwnership()
        {
            StaTest.Run(() =>
            {
                using var manager = new RegionManager();
                var host = new ContentControl();
                try
                {
                    manager.RegisterRegion("main", host);
                    Region.SetRegionName(host, "main");
                    host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

                    Assert.Same(host, manager.GetRegion("main"));
                    Assert.True(manager.UnregisterRegion("main", host));
                    Assert.Null(manager.GetRegion("main"));

                    host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Assert.Same(host, manager.GetRegion("main"));
                }
                finally
                {
                    host.ClearValue(Region.RegionNameProperty);
                }
            });
        }

        [Fact]
        public void DuplicateAttachedRegionNameIsRejected()
        {
            StaTest.Run(() =>
            {
                var first = new ContentControl();
                var second = new ContentControl();
                try
                {
                    Region.SetRegionName(first, "main");
                    Assert.Throws<InvalidOperationException>(
                        () => Region.SetRegionName(second, "main"));
                }
                finally
                {
                    first.ClearValue(Region.RegionNameProperty);
                    second.ClearValue(Region.RegionNameProperty);
                }
            });
        }

        [Fact]
        public void DeclarationCatalogDoesNotKeepAnAbandonedHostAlive()
        {
            StaTest.Run(() =>
            {
                var weakHost = DeclareTemporaryHost();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Assert.False(weakHost.IsAlive);
                using var manager = new RegionManager();
                Assert.Null(manager.GetRegion("temporary-declaration"));
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference DeclareTemporaryHost()
        {
            var host = new ContentControl();
            Region.SetRegionName(host, "temporary-declaration");
            return new WeakReference(host);
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~RegionTests --no-restore
```

Expected: build fails because `SimpleNavigation.Services.Region` does not exist and `RegionManager` is not disposable.

- [ ] **Step 3: Implement the weak declaration catalog**

Create `Services/Region.cs` with:

```csharp
using SimpleNavigation.Common;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Threading;

namespace SimpleNavigation.Services
{
    public static class Region
    {
        private static readonly object SyncRoot = new();
        private static readonly List<Declaration> Declarations = new();
        private static long nextActivationToken;

        internal static event EventHandler<RegionRegistrationChangedEventArgs>? RegistrationChanged;

        public static string? GetRegionName(DependencyObject obj) =>
            (string?)obj.GetValue(RegionNameProperty);

        public static void SetRegionName(DependencyObject obj, string value)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            ValidateName(value);
            if (obj is not FrameworkElement region)
                throw new ArgumentException(
                    "RegionName can only be attached to FrameworkElement instances.",
                    nameof(obj));
            RegionHostAdapterResolver.GetRequired(region);
            ValidateNameAvailable(value, region);
            obj.SetValue(RegionNameProperty, value);
        }

        public static readonly DependencyProperty RegionNameProperty =
            DependencyProperty.RegisterAttached(
                "RegionName",
                typeof(string),
                typeof(Region),
                new PropertyMetadata(null, OnRegionNameChanged));

        internal static IReadOnlyList<RegionRegistration> GetActiveRegistrations()
        {
            lock (SyncRoot)
            {
                var result = new List<RegionRegistration>();
                for (var index = Declarations.Count - 1; index >= 0; index--)
                {
                    var declaration = Declarations[index];
                    if (!declaration.Region.TryGetTarget(out var region))
                    {
                        Declarations.RemoveAt(index);
                        continue;
                    }

                    if (declaration.IsActive)
                        result.Add(new RegionRegistration(
                            declaration.Name,
                            region,
                            declaration.ActivationToken));
                }

                return result;
            }
        }

        private static void OnRegionNameChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is not FrameworkElement region)
                throw new ArgumentException("RegionName can only be attached to FrameworkElement instances.");

            var newName = args.NewValue as string;
            if (newName != null)
            {
                ValidateName(newName);
                RegionHostAdapterResolver.GetRequired(region);
                ValidateNameAvailable(newName, region);
            }

            var oldName = args.OldValue as string;
            if (oldName != null)
            {
                region.Loaded -= OnLoaded;
                region.Unloaded -= OnUnloaded;
                RemoveDeclaration(oldName, region);
            }

            if (newName == null)
                return;

            region.Loaded += OnLoaded;
            region.Unloaded += OnUnloaded;
            AddOrActivateDeclaration(newName, region);
        }

        private static void OnLoaded(object sender, RoutedEventArgs args)
        {
            var region = (FrameworkElement)sender;
            var name = GetRegionName(region);
            if (name != null)
                AddOrActivateDeclaration(name, region);
        }

        private static void OnUnloaded(object sender, RoutedEventArgs args)
        {
            var region = (FrameworkElement)sender;
            var name = GetRegionName(region);
            if (name != null)
                DeactivateDeclaration(name, region);
        }

        private static void AddOrActivateDeclaration(string name, FrameworkElement region)
        {
            long activationToken;
            lock (SyncRoot)
            {
                EnsureNameAvailableNoLock(name, region);
                var declaration = Find(region);
                if (declaration == null)
                {
                    declaration = new Declaration(
                        name,
                        region,
                        Interlocked.Increment(ref nextActivationToken));
                    Declarations.Add(declaration);
                }
                else
                {
                    declaration.Name = name;
                    if (declaration.IsActive)
                        return;
                    declaration.IsActive = true;
                    declaration.ActivationToken =
                        Interlocked.Increment(ref nextActivationToken);
                }
                activationToken = declaration.ActivationToken;
            }

            Publish(new RegionRegistrationChangedEventArgs(
                    name,
                    region,
                    activationToken,
                    true));
        }

        private static void DeactivateDeclaration(string name, FrameworkElement region)
        {
            long activationToken;
            lock (SyncRoot)
            {
                var declaration = Find(region);
                if (declaration == null || !declaration.IsActive)
                    return;
                activationToken = declaration.ActivationToken;
                declaration.IsActive = false;
            }

            Publish(new RegionRegistrationChangedEventArgs(
                    name,
                    region,
                    activationToken,
                    false));
        }

        private static void RemoveDeclaration(string name, FrameworkElement region)
        {
            var notify = false;
            long activationToken = 0;
            lock (SyncRoot)
            {
                var declaration = Find(region);
                if (declaration == null)
                    return;
                notify = declaration.IsActive;
                activationToken = declaration.ActivationToken;
                Declarations.Remove(declaration);
            }

            if (notify)
                Publish(new RegionRegistrationChangedEventArgs(
                        name,
                        region,
                        activationToken,
                        false));
        }

        private static Declaration? Find(FrameworkElement region)
        {
            foreach (var declaration in Declarations)
            {
                if (declaration.Region.TryGetTarget(out var target) &&
                    ReferenceEquals(target, region))
                    return declaration;
            }

            return null;
        }

        private static void ValidateNameAvailable(
            string name,
            FrameworkElement region)
        {
            lock (SyncRoot)
                EnsureNameAvailableNoLock(name, region);
        }

        private static void EnsureNameAvailableNoLock(
            string name,
            FrameworkElement region)
        {
            for (var index = Declarations.Count - 1; index >= 0; index--)
            {
                var declaration = Declarations[index];
                if (!declaration.Region.TryGetTarget(out var existing))
                {
                    Declarations.RemoveAt(index);
                    continue;
                }

                if (declaration.IsActive &&
                    string.Equals(declaration.Name, name, StringComparison.Ordinal) &&
                    !ReferenceEquals(existing, region))
                    throw new InvalidOperationException(
                        $"Region '{name}' is already declared by '{existing.GetType().FullName}'.");
            }
        }

        private static void Publish(RegionRegistrationChangedEventArgs args)
        {
            var invocationList = RegistrationChanged?.GetInvocationList();
            if (invocationList == null)
                return;

            Exception? firstFailure = null;
            foreach (var callback in invocationList)
            {
                var handler = (EventHandler<RegionRegistrationChangedEventArgs>)callback;
                try
                {
                    handler(null, args);
                }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                }
            }

            if (firstFailure != null)
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Region name cannot be null or whitespace.", nameof(name));
        }

        private sealed class Declaration
        {
            public Declaration(
                string name,
                FrameworkElement region,
                long activationToken)
            {
                Name = name;
                Region = new WeakReference<FrameworkElement>(region);
                ActivationToken = activationToken;
                IsActive = true;
            }

            public string Name { get; set; }
            public WeakReference<FrameworkElement> Region { get; }
            public long ActivationToken { get; set; }
            public bool IsActive { get; set; }
        }
    }

    internal sealed class RegionRegistration
    {
        public RegionRegistration(
            string name,
            FrameworkElement region,
            long activationToken)
        {
            Name = name;
            Region = region;
            ActivationToken = activationToken;
        }

        public string Name { get; }
        public FrameworkElement Region { get; }
        public long ActivationToken { get; }
    }

    internal sealed class RegionRegistrationChangedEventArgs : EventArgs
    {
        public RegionRegistrationChangedEventArgs(
            string name,
            FrameworkElement region,
            long activationToken,
            bool isRegistered)
        {
            Name = name;
            Region = region;
            ActivationToken = activationToken;
            IsRegistered = isRegistered;
        }

        public string Name { get; }
        public FrameworkElement Region { get; }
        public long ActivationToken { get; }
        public bool IsRegistered { get; }
    }
}
```

- [ ] **Step 4: Subscribe RegionManager before importing the snapshot**

Update `RegionManager` to implement `IDisposable`, subscribe before importing `Region.GetActiveRegistrations()`, and track attached activation tokens independently from public programmatic ownership:

Add this field beside `syncRoot`:

```csharp
private bool isDisposed;
```

```csharp
public RegionManager()
{
    Region.RegistrationChanged += OnRegistrationChanged;
    try
    {
        foreach (var registration in Region.GetActiveRegistrations())
            RegisterDeclaration(
                registration.Name,
                registration.Region,
                registration.ActivationToken);
    }
    catch
    {
        Region.RegistrationChanged -= OnRegistrationChanged;
        throw;
    }
}

private void OnRegistrationChanged(
    object? sender,
    RegionRegistrationChangedEventArgs args)
{
    if (args.IsRegistered)
        RegisterDeclaration(args.Name, args.Region, args.ActivationToken);
    else
        UnregisterDeclaration(args.Name, args.Region, args.ActivationToken);
}

private void RegisterDeclaration(
    string regionName,
    FrameworkElement region,
    long activationToken)
{
    ValidateName(regionName);
    RegionHostAdapterResolver.GetRequired(region);

    lock (syncRoot)
    {
        if (isDisposed)
            return;

        RegionEntry entry;
        if (regions.TryGetValue(regionName, out var existing) &&
            existing.Region.TryGetTarget(out var existingRegion))
        {
            if (!ReferenceEquals(existingRegion, region))
                throw new InvalidOperationException(
                    $"Region '{regionName}' is already registered by '{existingRegion.GetType().FullName}'.");
            entry = existing;
        }
        else
        {
            entry = new RegionEntry(region);
            regions[regionName] = entry;
        }

        entry.DeclarationTokens.Add(activationToken);
    }
}

private void UnregisterDeclaration(
    string regionName,
    FrameworkElement region,
    long activationToken)
{
    lock (syncRoot)
    {
        if (isDisposed)
            return;

        if (!regions.TryGetValue(regionName, out var entry) ||
            !entry.Region.TryGetTarget(out var existingRegion) ||
            !ReferenceEquals(existingRegion, region))
            return;

        entry.DeclarationTokens.Remove(activationToken);
        if (!entry.IsProgrammatic && entry.DeclarationTokens.Count == 0)
            regions.Remove(regionName);
    }
}

public void Dispose()
{
    Region.RegistrationChanged -= OnRegistrationChanged;
    lock (syncRoot)
    {
        if (isDisposed)
            return;
        isDisposed = true;
        regions.Clear();
    }
}
```

Add `using SimpleNavigation.Services;` and change the declaration to:

```csharp
public sealed class RegionManager : IRegionManager, IDisposable
```

- [ ] **Step 5: Run region lifecycle tests on both targets**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj --filter "FullyQualifiedName~RegionTests|FullyQualifiedName~RegionManagerTests" --no-restore
```

Expected: all region tests pass for both target frameworks.

- [ ] **Step 6: Commit declarative region support**

```powershell
git add Services/Region.cs Common/RegionManager.cs Tests/SimpleNavigation.Tests/RegionTests.cs
git commit -m "feat: add Region attached registration"
```

### Task 4: Add Explicit Page And Content Route Registration

**Files:**
- Create: `Common/NavigationRouteRegistry.cs`
- Modify: `Extensions/NavigationExtensions.cs`
- Create: `Tests/SimpleNavigation.Tests/TestTypes.cs`
- Create: `Tests/SimpleNavigation.Tests/NavigationExtensionsTests.cs`

- [ ] **Step 1: Add reusable test view types**

Create `TestTypes.cs`:

```csharp
using SimpleNavigation.Common;
using System.Windows.Controls;

namespace SimpleNavigation.Tests
{
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

}
```

- [ ] **Step 2: Write failing extension tests**

Create `NavigationExtensionsTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Extensions;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Windows;

namespace SimpleNavigation.Tests
{
    public class NavigationExtensionsTests
    {
        [Fact]
        public void SingleGenericKeyOverloadsRegisterTransientViews()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddPage<FirstPage>("page");
                services.AddContent<TestContent>("content");
                using var provider = services.BuildServiceProvider();

                Assert.NotSame(
                    provider.GetRequiredService<FirstPage>(),
                    provider.GetRequiredService<FirstPage>());
                Assert.NotSame(
                    provider.GetRequiredService<TestContent>(),
                    provider.GetRequiredService<TestContent>());
            });
        }

        [Fact]
        public void DoubleGenericOverloadsRegisterViewAndViewModel()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.AddPage<FirstPage, TestViewModel>();
                services.AddContent<TestContent, TestViewModel>("content");
                using var provider = services.BuildServiceProvider();

                Assert.IsType<FirstPage>(provider.GetRequiredService<FirstPage>());
                var content = provider.GetRequiredService<TestContent>();
                Assert.IsType<TestContent>(content);
                Assert.Null(content.DataContext);
                Assert.IsType<TestViewModel>(provider.GetRequiredService<TestViewModel>());
            });
        }

        [Fact]
        public void ExistingSingletonRegistrationIsPreserved()
        {
            StaTest.Run(() =>
            {
                var page = new FirstPage();
                var services = new ServiceCollection();
                services.AddSingleton(page);
                services.AddPage<FirstPage>("page");
                using var provider = services.BuildServiceProvider();

                Assert.Same(page, provider.GetRequiredService<FirstPage>());
            });
        }

        [Fact]
        public void DuplicateKeyWithinOneCategoryIsRejected()
        {
            var services = new ServiceCollection();
            services.AddPage<FirstPage>("main");

            Assert.Throws<ArgumentException>(
                () => services.AddPage<SecondPage>("main"));
        }

        [Fact]
        public void TheSameKeyCanExistInPageAndContentMaps()
        {
            var services = new ServiceCollection();

            services.AddPage<FirstPage>("main");
            services.AddContent<TestContent>("main");
        }

        [Fact]
        public void ContentRegistrationRejectsPageAndWindowTypes()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentException>(
                () => services.AddContent<FirstPage>("page"));
            Assert.Throws<ArgumentException>(
                () => services.AddContent<Window>("window"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void InvalidRouteKeyIsRejected(string key)
        {
            Assert.Throws<ArgumentException>(
                () => new ServiceCollection().AddPage<FirstPage>(key));
        }
    }
}
```

- [ ] **Step 3: Run extension tests and verify RED**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~NavigationExtensionsTests --no-restore
```

Expected: build fails because the six `AddPage` and `AddContent` methods do not exist.

- [ ] **Step 4: Implement immutable route descriptors and maps**

Create `Common/NavigationRouteRegistry.cs`:

```csharp
namespace SimpleNavigation.Common
{
    internal enum NavigationRouteKind
    {
        Page,
        Content
    }

    internal sealed class NavigationRouteRegistration
    {
        public NavigationRouteRegistration(
            NavigationRouteKind kind,
            string key,
            Type targetType)
        {
            Kind = kind;
            Key = key;
            TargetType = targetType;
        }

        public NavigationRouteKind Kind { get; }
        public string Key { get; }
        public Type TargetType { get; }
    }

    internal sealed class NavigationRouteRegistry
    {
        private readonly IReadOnlyDictionary<string, Type> pages;
        private readonly IReadOnlyDictionary<string, Type> contents;

        public NavigationRouteRegistry(IEnumerable<NavigationRouteRegistration> registrations)
        {
            pages = Build(registrations, NavigationRouteKind.Page);
            contents = Build(registrations, NavigationRouteKind.Content);
        }

        public Type GetRequiredPageType(string key) =>
            GetRequired(pages, key, "page");

        public Type GetRequiredContentType(string key) =>
            GetRequired(contents, key, "content");

        private static IReadOnlyDictionary<string, Type> Build(
            IEnumerable<NavigationRouteRegistration> registrations,
            NavigationRouteKind kind)
        {
            var routes = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var registration in registrations.Where(item => item.Kind == kind))
                routes.Add(registration.Key, registration.TargetType);
            return routes;
        }

        private static Type GetRequired(
            IReadOnlyDictionary<string, Type> routes,
            string key,
            string category)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Route key cannot be null or whitespace.", nameof(key));

            if (routes.TryGetValue(key, out var targetType))
                return targetType;

            throw new KeyNotFoundException(
                $"No {category} route is registered for key '{key}'.");
        }
    }
}
```

- [ ] **Step 5: Add the six extension overloads**

In `NavigationExtensions`, add the following public methods and private helpers. Preserve the existing `TryAddSingleton` registrations for dialogs and pages.

```csharp
public static IServiceCollection AddPage<TPage>(
    this IServiceCollection services,
    string key)
    where TPage : Page
{
    services.TryAddTransient<TPage>();
    AddRoute(services, NavigationRouteKind.Page, key, typeof(TPage));
    return services;
}

public static IServiceCollection AddPage<TPage, TViewModel>(
    this IServiceCollection services)
    where TPage : Page
    where TViewModel : class
{
    services.TryAddTransient<TPage>();
    services.TryAddTransient<TViewModel>();
    return services;
}

public static IServiceCollection AddPage<TPage, TViewModel>(
    this IServiceCollection services,
    string key)
    where TPage : Page
    where TViewModel : class
{
    services.AddPage<TPage, TViewModel>();
    AddRoute(services, NavigationRouteKind.Page, key, typeof(TPage));
    return services;
}

public static IServiceCollection AddContent<TView>(
    this IServiceCollection services,
    string key)
    where TView : FrameworkElement
{
    ValidateContentType(typeof(TView));
    services.TryAddTransient<TView>();
    AddRoute(services, NavigationRouteKind.Content, key, typeof(TView));
    return services;
}

public static IServiceCollection AddContent<TView, TViewModel>(
    this IServiceCollection services)
    where TView : FrameworkElement
    where TViewModel : class
{
    ValidateContentType(typeof(TView));
    services.TryAddTransient<TView>();
    services.TryAddTransient<TViewModel>();
    return services;
}

public static IServiceCollection AddContent<TView, TViewModel>(
    this IServiceCollection services,
    string key)
    where TView : FrameworkElement
    where TViewModel : class
{
    services.AddContent<TView, TViewModel>();
    AddRoute(services, NavigationRouteKind.Content, key, typeof(TView));
    return services;
}

private static void AddRoute(
    IServiceCollection services,
    NavigationRouteKind kind,
    string key,
    Type targetType)
{
    if (string.IsNullOrWhiteSpace(key))
        throw new ArgumentException("Route key cannot be null or whitespace.", nameof(key));

    var duplicate = services.Any(descriptor =>
        descriptor.ServiceType == typeof(NavigationRouteRegistration) &&
        descriptor.ImplementationInstance is NavigationRouteRegistration route &&
        route.Kind == kind &&
        string.Equals(route.Key, key, StringComparison.Ordinal));

    if (duplicate)
        throw new ArgumentException(
            $"A {kind.ToString().ToLowerInvariant()} route with key '{key}' is already registered.",
            nameof(key));

    services.AddSingleton(new NavigationRouteRegistration(kind, key, targetType));
}

private static void ValidateContentType(Type targetType)
{
    if (typeof(Page).IsAssignableFrom(targetType) ||
        typeof(Window).IsAssignableFrom(targetType))
        throw new ArgumentException(
            $"Content type '{targetType.FullName}' cannot derive from Page or Window.",
            nameof(targetType));
}
```

Add these core registrations inside `RegisterNavigationService`:

```csharp
serviceCollection.TryAddSingleton<IRegionManager, RegionManager>();
serviceCollection.TryAddSingleton<NavigationRouteRegistry>(provider =>
    new NavigationRouteRegistry(
        provider.GetServices<NavigationRouteRegistration>()));
```

Add explicit usings for `System.Windows` and `System.Windows.Controls` so both target frameworks compile.

- [ ] **Step 6: Run extension and region tests**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj --filter "FullyQualifiedName~NavigationExtensionsTests|FullyQualifiedName~Region" --no-restore
```

Expected: all selected tests pass for both target frameworks.

- [ ] **Step 7: Commit explicit route registration**

```powershell
git add Common/NavigationRouteRegistry.cs Extensions/NavigationExtensions.cs Tests/SimpleNavigation.Tests/TestTypes.cs Tests/SimpleNavigation.Tests/NavigationExtensionsTests.cs
git commit -m "feat: add explicit navigation routes"
```

### Task 5: Refactor PageService Around RegionManager

**Files:**
- Create: `Interface/INavigationAware.cs`
- Create: `Common/NavigationAwareNotifier.cs`
- Modify: `Interface/IPageAware.cs`
- Modify: `Interface/IPageService.cs`
- Modify: `Common/RegionHostAdapter.cs`
- Modify: `Common/FrameRegionAdapter.cs`
- Replace: `Services/PageService.cs`
- Delete: `Services/RegionService.cs`
- Modify: `Tests/SimpleNavigation.Tests/TestTypes.cs`
- Create: `Tests/SimpleNavigation.Tests/PageServiceTests.cs`

- [ ] **Step 1: Add awareness fixtures and failing page navigation tests**

Add `using SimpleNavigation.Interface;` to `TestTypes.cs`, then add these two types:

```csharp
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
    public void OnNavigated(DialogParameters? parameters) =>
        throw new InvalidOperationException("awareness failed");
}
```

Create `PageServiceTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SimpleNavigation.Tests
{
    public class PageServiceTests
    {
        [Fact]
        public void GenericAndTypeNavigationDoNotRequireRoutes()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddTransient<FirstPage>();
                services.AddTransient<SecondPage>();
                using var provider = services.BuildServiceProvider();
                var frame = RegisterFrame(provider, "main");
                var service = provider.GetRequiredService<IPageService>();

                service.Navigate<FirstPage>("main");
                StaTest.PumpUntil(() => frame.Content is FirstPage);
                Assert.IsType<FirstPage>(frame.Content);

                service.Navigate("main", typeof(SecondPage));
                StaTest.PumpUntil(() => frame.Content is SecondPage);
                Assert.IsType<SecondPage>(frame.Content);
            });
        }

        [Fact]
        public void StringNavigationUsesRouteThenOrdinaryDi()
        {
            StaTest.Run(() =>
            {
                var expected = new FirstPage();
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(expected);
                services.AddPage<FirstPage>("first");
                using var provider = services.BuildServiceProvider();
                var frame = RegisterFrame(provider, "main");

                provider.GetRequiredService<IPageService>()
                    .Navigate("main", "first");

                StaTest.PumpUntil(() => ReferenceEquals(frame.Content, expected));
                Assert.Same(expected, frame.Content);
                Assert.Throws<KeyNotFoundException>(
                    () => provider.GetRequiredService<IPageService>()
                        .Navigate("main", "First"));
            });
        }

        [Fact]
        public void ViewAndDataContextReceiveParametersOnceEach()
        {
            StaTest.Run(() =>
            {
                var viewModel = new AwareViewModel();
                var page = new AwarePage { DataContext = viewModel };
                var parameters = new DialogParameters("id", 7);
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(page);
                using var provider = services.BuildServiceProvider();
                var frame = RegisterFrame(provider, "main");

                provider.GetRequiredService<IPageService>()
                    .Navigate<AwarePage>("main", parameters);

                Assert.Equal(1, page.CallCount);
                Assert.Equal(1, viewModel.CallCount);
                Assert.Same(parameters, page.Parameters);
                Assert.Same(parameters, viewModel.Parameters);
                GC.KeepAlive(frame);
            });
        }

        [Fact]
        public void NullParametersStillTriggerAwarenessAndSameInstanceIsDeduplicated()
        {
            StaTest.Run(() =>
            {
                var page = new AwarePage();
                page.DataContext = page;
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(page);
                using var provider = services.BuildServiceProvider();
                var frame = RegisterFrame(provider, "main");

                provider.GetRequiredService<IPageService>()
                    .Navigate<AwarePage>("main");

                Assert.Equal(1, page.CallCount);
                Assert.Null(page.Parameters);
                GC.KeepAlive(frame);
            });
        }

        [Fact]
        public void MissingWrongAndUnknownInputsFailFast()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddTransient<FirstPage>();
                using var provider = services.BuildServiceProvider();
                var contentHost = new ContentControl();
                provider.GetRequiredService<IRegionManager>()
                    .RegisterRegion("content", contentHost);
                var service = provider.GetRequiredService<IPageService>();

                Assert.Throws<InvalidOperationException>(
                    () => service.Navigate<FirstPage>("missing"));
                Assert.Throws<InvalidOperationException>(
                    () => service.Navigate<FirstPage>("content"));
                Assert.Throws<ArgumentException>(
                    () => service.Navigate("content", typeof(TestContent)));
                Assert.Throws<KeyNotFoundException>(
                    () => service.Navigate("content", "unknown"));
                GC.KeepAlive(contentHost);
            });
        }

        [Fact]
        public void GoBackUsesTheFrameJournalAndLegacyMethodForwards()
        {
            StaTest.Run(() =>
            {
                var first = new FirstPage();
                var second = new SecondPage();
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(first);
                services.AddSingleton(second);
                using var provider = services.BuildServiceProvider();
                var frame = RegisterFrame(provider, "main");
                var service = provider.GetRequiredService<IPageService>();

                service.Navigate<FirstPage>("main");
                StaTest.PumpUntil(() => ReferenceEquals(frame.Content, first));
                service.Navigate<SecondPage>("main");
                StaTest.PumpUntil(() => ReferenceEquals(frame.Content, second));
                Assert.True(frame.CanGoBack);

                service.GoBack("main");
                StaTest.PumpUntil(() => ReferenceEquals(frame.Content, first));
                Assert.Same(first, frame.Content);

#pragma warning disable CS0618
                service.Goback("main");
#pragma warning restore CS0618
            });
        }

        [Fact]
        public void MissingDiRegistrationKeepsTheContainerException()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                using var provider = services.BuildServiceProvider();
                var frame = RegisterFrame(provider, "main");

                var exception = Assert.Throws<InvalidOperationException>(
                    () => provider.GetRequiredService<IPageService>()
                        .Navigate<FirstPage>("main"));

                Assert.Contains(typeof(FirstPage).FullName!, exception.Message);
                GC.KeepAlive(frame);
            });
        }

        [Fact]
        public void AwarenessExceptionPropagatesToTheCaller()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(new ThrowingAwarePage());
                using var provider = services.BuildServiceProvider();
                var frame = RegisterFrame(provider, "main");

                var exception = Assert.Throws<InvalidOperationException>(
                    () => provider.GetRequiredService<IPageService>()
                        .Navigate<ThrowingAwarePage>("main"));

                Assert.Equal("awareness failed", exception.Message);
                GC.KeepAlive(frame);
            });
        }

        [Fact]
        public void NavigationRequiresTheHostDispatcherThread()
        {
            ServiceProvider? provider = null;
            IPageService? service = null;
            Frame? frame = null;
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(new FirstPage());
                provider = services.BuildServiceProvider();
                frame = RegisterFrame(provider, "main");
                service = provider.GetRequiredService<IPageService>();
            });

            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => service!.Navigate<FirstPage>("main"));
                GC.KeepAlive(frame);
            }
            finally
            {
                provider!.Dispose();
            }
        }

        private static Frame RegisterFrame(IServiceProvider provider, string name)
        {
            var frame = new Frame
            {
                JournalOwnership = JournalOwnership.OwnsJournal,
                NavigationUIVisibility = NavigationUIVisibility.Hidden
            };
            provider.GetRequiredService<IRegionManager>().RegisterRegion(name, frame);
            return frame;
        }
    }
}
```

- [ ] **Step 2: Run page tests and verify RED**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~PageServiceTests --no-restore
```

Expected: build fails because `INavigationAware`, string navigation, and `GoBack` do not exist.

- [ ] **Step 3: Add the shared awareness contract and notifier**

Create `Interface/INavigationAware.cs`:

```csharp
using SimpleNavigation.Common;

namespace SimpleNavigation.Interface
{
    public interface INavigationAware
    {
        void OnNavigated(DialogParameters? parameters);
    }
}
```

Change `IPageAware` to inherit `INavigationAware`, retain `Receive`, and remove its duplicate `OnNavigated` declaration:

```csharp
public interface IPageAware : INavigationAware
{
    event Action<DialogParameters?>? Receive;
}
```

Create `Common/NavigationAwareNotifier.cs`:

```csharp
using SimpleNavigation.Interface;
using System.Windows;

namespace SimpleNavigation.Common
{
    internal static class NavigationAwareNotifier
    {
        public static void Notify(
            FrameworkElement target,
            DialogParameters? parameters)
        {
            if (target is INavigationAware targetAware)
                targetAware.OnNavigated(parameters);

            var dataContext = target.DataContext;
            if (dataContext is INavigationAware contextAware &&
                !ReferenceEquals(dataContext, target))
                contextAware.OnNavigated(parameters);
        }
    }
}
```

- [ ] **Step 4: Add frame navigation capability to the adapter**

Add this interface to `RegionHostAdapter.cs`:

```csharp
internal interface IPageRegionHostAdapter : IRegionHostAdapter
{
    bool Navigate(Frame frame, Page page);
    bool CanGoBack(Frame frame);
    void GoBack(Frame frame);
}
```

Add `using System.Windows.Controls;`. Change `FrameRegionAdapter` to implement it:

```csharp
internal sealed class FrameRegionAdapter : IPageRegionHostAdapter
{
    public RegionHostKind Kind => RegionHostKind.Page;

    public bool CanHandle(FrameworkElement region) => region is Frame;

    public bool Navigate(Frame frame, Page page)
    {
        frame.Dispatcher.VerifyAccess();
        return frame.Navigate(page);
    }

    public bool CanGoBack(Frame frame)
    {
        frame.Dispatcher.VerifyAccess();
        return frame.CanGoBack;
    }

    public void GoBack(Frame frame)
    {
        frame.Dispatcher.VerifyAccess();
        frame.GoBack();
    }
}
```

- [ ] **Step 5: Replace the page contract and implementation**

Replace `IPageService` with the exact public API approved in the design:

```csharp
using SimpleNavigation.Common;
using System.Windows.Controls;

namespace SimpleNavigation.Interface
{
    public interface IPageService
    {
        void Navigate<TPage>(string regionName, DialogParameters? parameters = null)
            where TPage : Page;
        void Navigate(string regionName, Type targetType, DialogParameters? parameters = null);
        void Navigate(string regionName, string key, DialogParameters? parameters = null);
        void GoBack(string regionName);

        [Obsolete("Use GoBack instead.")]
        void Goback(string regionName);
    }
}
```

Replace `PageService.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Services
{
    public class PageService : IPageService
    {
        private readonly IServiceProvider provider;
        private readonly IRegionManager regionManager;
        private readonly NavigationRouteRegistry routes;

        public PageService(IServiceProvider provider, IRegionManager regionManager)
        {
            this.provider = provider;
            this.regionManager = regionManager;
            routes = provider.GetRequiredService<NavigationRouteRegistry>();
        }

        public void Navigate<TPage>(
            string regionName,
            DialogParameters? parameters = null)
            where TPage : Page
        {
            ValidateRegionName(regionName);
            NavigateCore(regionName, provider.GetRequiredService<TPage>(), parameters);
        }

        public void Navigate(
            string regionName,
            Type targetType,
            DialogParameters? parameters = null)
        {
            ValidateRegionName(regionName);
            ValidatePageType(targetType);
            var page = provider.GetRequiredService(targetType) as Page
                ?? throw new InvalidOperationException(
                    $"Resolved service '{targetType.FullName}' is not a Page.");
            NavigateCore(regionName, page, parameters);
        }

        public void Navigate(
            string regionName,
            string key,
            DialogParameters? parameters = null)
        {
            ValidateRegionName(regionName);
            var targetType = routes.GetRequiredPageType(key);
            var page = provider.GetRequiredService(targetType) as Page
                ?? throw new InvalidOperationException(
                    $"Resolved service '{targetType.FullName}' is not a Page.");
            NavigateCore(regionName, page, parameters);
        }

        public void GoBack(string regionName)
        {
            var frame = GetRequiredFrame(regionName);
            var adapter = (IPageRegionHostAdapter)RegionHostAdapterResolver.GetRequired(frame);
            if (adapter.CanGoBack(frame))
                adapter.GoBack(frame);
        }

        [Obsolete("Use GoBack instead.")]
        public void Goback(string regionName) => GoBack(regionName);

        private void NavigateCore(
            string regionName,
            Page page,
            DialogParameters? parameters)
        {
            var frame = GetRequiredFrame(regionName);
            var adapter = (IPageRegionHostAdapter)RegionHostAdapterResolver.GetRequired(frame);
            if (adapter.Navigate(frame, page))
                NavigationAwareNotifier.Notify(page, parameters);
        }

        private Frame GetRequiredFrame(string regionName)
        {
            ValidateRegionName(regionName);
            var region = regionManager.GetRegion(regionName);
            if (region is Frame frame)
                return frame;

            var actual = region?.GetType().FullName ?? "missing";
            throw new InvalidOperationException(
                $"Region '{regionName}' must be a Frame but was '{actual}'.");
        }

        private static void ValidatePageType(Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (!typeof(Page).IsAssignableFrom(targetType))
                throw new ArgumentException(
                    $"Target type '{targetType.FullName}' must derive from Page.",
                    nameof(targetType));
        }

        private static void ValidateRegionName(string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                throw new ArgumentException("Region name cannot be null or whitespace.", nameof(regionName));
        }
    }
}
```

Delete `Services/RegionService.cs`; the new `Region` type is now the only attached-property owner.

- [ ] **Step 6: Run page, region, and extension tests**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj --filter "FullyQualifiedName~PageServiceTests|FullyQualifiedName~Region|FullyQualifiedName~NavigationExtensionsTests" --no-restore
```

Expected: all selected tests pass on both target frameworks and no source file references `RegionService`.

- [ ] **Step 7: Commit the page-service refactor**

```powershell
git add Interface/INavigationAware.cs Interface/IPageAware.cs Interface/IPageService.cs Common/NavigationAwareNotifier.cs Common/RegionHostAdapter.cs Common/FrameRegionAdapter.cs Services/PageService.cs Services/RegionService.cs Tests/SimpleNavigation.Tests/TestTypes.cs Tests/SimpleNavigation.Tests/PageServiceTests.cs
git commit -m "feat: rebuild page navigation around regions"
```

### Task 6: Add ContentService For Non-Page FrameworkElements

**Files:**
- Create: `Interface/IContentService.cs`
- Create: `Services/ContentService.cs`
- Modify: `Common/RegionHostAdapter.cs`
- Modify: `Common/ContentControlRegionAdapter.cs`
- Modify: `Extensions/NavigationExtensions.cs`
- Modify: `Tests/SimpleNavigation.Tests/TestTypes.cs`
- Create: `Tests/SimpleNavigation.Tests/ContentServiceTests.cs`

- [ ] **Step 1: Add an aware content fixture**

Append to `TestTypes.cs`:

```csharp
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
```

- [ ] **Step 2: Write failing content navigation tests**

Create `ContentServiceTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Extensions;
using SimpleNavigation.Interface;
using SimpleNavigation.Tests.TestInfrastructure;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Tests
{
    public class ContentServiceTests
    {
        [Fact]
        public void GenericAndTypeNavigationDoNotRequireRoutes()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddTransient<TestContent>();
                services.AddTransient<Grid>();
                using var provider = services.BuildServiceProvider();
                var host = RegisterContentHost(provider, "main");
                var service = provider.GetRequiredService<IContentService>();

                service.Navigate<TestContent>("main");
                Assert.IsType<TestContent>(host.Content);

                service.Navigate("main", typeof(Grid));
                Assert.IsType<Grid>(host.Content);
            });
        }

        [Fact]
        public void StringNavigationUsesRouteThenOrdinaryDi()
        {
            StaTest.Run(() =>
            {
                var expected = new TestContent();
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(expected);
                services.AddContent<TestContent>("content");
                using var provider = services.BuildServiceProvider();
                var host = RegisterContentHost(provider, "main");

                provider.GetRequiredService<IContentService>()
                    .Navigate("main", "content");

                Assert.Same(expected, host.Content);
            });
        }

        [Fact]
        public void ViewAndDataContextReceiveParametersOnceEach()
        {
            StaTest.Run(() =>
            {
                var viewModel = new AwareViewModel();
                var content = new AwareContent { DataContext = viewModel };
                var parameters = new DialogParameters("id", 9);
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddSingleton(content);
                using var provider = services.BuildServiceProvider();
                var host = RegisterContentHost(provider, "main");

                provider.GetRequiredService<IContentService>()
                    .Navigate<AwareContent>("main", parameters);

                Assert.Equal(1, content.CallCount);
                Assert.Equal(1, viewModel.CallCount);
                Assert.Same(parameters, content.Parameters);
                Assert.Same(parameters, viewModel.Parameters);
                GC.KeepAlive(host);
            });
        }

        [Fact]
        public void PageWindowAndFrameHostAreRejected()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddTransient<FirstPage>();
                services.AddTransient<Window>();
                services.AddTransient<TestContent>();
                using var provider = services.BuildServiceProvider();
                var frame = new Frame();
                provider.GetRequiredService<IRegionManager>()
                    .RegisterRegion("frame", frame);
                var service = provider.GetRequiredService<IContentService>();

                Assert.Throws<ArgumentException>(
                    () => service.Navigate<FirstPage>("frame"));
                Assert.Throws<ArgumentException>(
                    () => service.Navigate("frame", typeof(Window)));
                Assert.Throws<InvalidOperationException>(
                    () => service.Navigate<TestContent>("frame"));
                GC.KeepAlive(frame);
            });
        }

        [Fact]
        public void MissingRegionUnknownKeyAndMissingDiFailFast()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddContent<TestContent>("known");
                using var provider = services.BuildServiceProvider();
                var service = provider.GetRequiredService<IContentService>();

                Assert.Throws<InvalidOperationException>(
                    () => service.Navigate<TestContent>("missing"));
                Assert.Throws<KeyNotFoundException>(
                    () => service.Navigate("missing", "unknown"));
                Assert.Throws<InvalidOperationException>(
                    () => service.Navigate("missing", typeof(Grid)));
            });
        }

        [Fact]
        public void DoubleGenericRegistrationWithoutKeyDoesNotCreateARoute()
        {
            StaTest.Run(() =>
            {
                var services = new ServiceCollection();
                services.RegisterNavigationService();
                services.AddContent<TestContent, TestViewModel>();
                using var provider = services.BuildServiceProvider();
                var host = RegisterContentHost(provider, "main");

                Assert.Throws<KeyNotFoundException>(
                    () => provider.GetRequiredService<IContentService>()
                        .Navigate("main", "content"));
                GC.KeepAlive(host);
            });
        }

        private static ContentControl RegisterContentHost(
            IServiceProvider provider,
            string name)
        {
            var host = new ContentControl();
            provider.GetRequiredService<IRegionManager>().RegisterRegion(name, host);
            return host;
        }
    }
}
```

- [ ] **Step 3: Run content tests and verify RED**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -f net8.0-windows --filter FullyQualifiedName~ContentServiceTests --no-restore
```

Expected: build fails because `IContentService` and `ContentService` do not exist.

- [ ] **Step 4: Add the public content contract**

Create `Interface/IContentService.cs`:

```csharp
using SimpleNavigation.Common;
using System.Windows;

namespace SimpleNavigation.Interface
{
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
}
```

- [ ] **Step 5: Add content presentation capability to the adapter**

Add to `RegionHostAdapter.cs`:

```csharp
internal interface IContentRegionHostAdapter : IRegionHostAdapter
{
    void Present(ContentControl host, FrameworkElement content);
}
```

Change `ContentControlRegionAdapter` to:

```csharp
internal sealed class ContentControlRegionAdapter : IContentRegionHostAdapter
{
    public RegionHostKind Kind => RegionHostKind.Content;

    public bool CanHandle(FrameworkElement region) =>
        region is ContentControl && region is not Frame;

    public void Present(ContentControl host, FrameworkElement content)
    {
        host.Dispatcher.VerifyAccess();
        host.Content = content;
    }
}
```

- [ ] **Step 6: Implement ContentService**

Create `Services/ContentService.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Common;
using SimpleNavigation.Interface;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNavigation.Services
{
    public class ContentService : IContentService
    {
        private readonly IServiceProvider provider;
        private readonly IRegionManager regionManager;
        private readonly NavigationRouteRegistry routes;

        public ContentService(IServiceProvider provider, IRegionManager regionManager)
        {
            this.provider = provider;
            this.regionManager = regionManager;
            routes = provider.GetRequiredService<NavigationRouteRegistry>();
        }

        public void Navigate<TContent>(
            string regionName,
            DialogParameters? parameters = null)
            where TContent : FrameworkElement
        {
            ValidateRegionName(regionName);
            ValidateContentType(typeof(TContent));
            NavigateCore(
                regionName,
                provider.GetRequiredService<TContent>(),
                parameters);
        }

        public void Navigate(
            string regionName,
            Type targetType,
            DialogParameters? parameters = null)
        {
            ValidateRegionName(regionName);
            ValidateContentType(targetType);
            var content = provider.GetRequiredService(targetType) as FrameworkElement
                ?? throw new InvalidOperationException(
                    $"Resolved service '{targetType.FullName}' is not a FrameworkElement.");
            NavigateCore(regionName, content, parameters);
        }

        public void Navigate(
            string regionName,
            string key,
            DialogParameters? parameters = null)
        {
            ValidateRegionName(regionName);
            var targetType = routes.GetRequiredContentType(key);
            ValidateContentType(targetType);
            var content = provider.GetRequiredService(targetType) as FrameworkElement
                ?? throw new InvalidOperationException(
                    $"Resolved service '{targetType.FullName}' is not a FrameworkElement.");
            NavigateCore(regionName, content, parameters);
        }

        private void NavigateCore(
            string regionName,
            FrameworkElement content,
            DialogParameters? parameters)
        {
            var host = GetRequiredHost(regionName);
            var adapter = (IContentRegionHostAdapter)
                RegionHostAdapterResolver.GetRequired(host);
            adapter.Present(host, content);
            NavigationAwareNotifier.Notify(content, parameters);
        }

        private ContentControl GetRequiredHost(string regionName)
        {
            var region = regionManager.GetRegion(regionName);
            if (region is ContentControl host && region is not Frame)
                return host;

            var actual = region?.GetType().FullName ?? "missing";
            throw new InvalidOperationException(
                $"Region '{regionName}' must be a non-Frame ContentControl but was '{actual}'.");
        }

        private static void ValidateContentType(Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (!typeof(FrameworkElement).IsAssignableFrom(targetType) ||
                typeof(Page).IsAssignableFrom(targetType) ||
                typeof(Window).IsAssignableFrom(targetType))
                throw new ArgumentException(
                    $"Target type '{targetType.FullName}' must be a non-Page, non-Window FrameworkElement.",
                    nameof(targetType));
        }

        private static void ValidateRegionName(string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                throw new ArgumentException("Region name cannot be null or whitespace.", nameof(regionName));
        }
    }
}
```

- [ ] **Step 7: Register ContentService and run the navigation suite**

Add this line to `RegisterNavigationService`:

```csharp
serviceCollection.TryAddSingleton<IContentService, ContentService>();
```

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj --no-restore
```

Expected: every test passes for both `net48` and `net8.0-windows`.

- [ ] **Step 8: Commit content navigation**

```powershell
git add Interface/IContentService.cs Services/ContentService.cs Common/RegionHostAdapter.cs Common/ContentControlRegionAdapter.cs Extensions/NavigationExtensions.cs Tests/SimpleNavigation.Tests/TestTypes.cs Tests/SimpleNavigation.Tests/ContentServiceTests.cs
git commit -m "feat: add content navigation service"
```

### Task 7: Update Documentation And Migration Guidance

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace the project structure and feature summary**

Update the README to describe four public capabilities: `PageService`, `ContentService`, `DialogService`, and `RegionManager`. Replace `RegionService.cs` in the tree with these entries:

```text
Interface/
  IRegionManager.cs
  IPageService.cs
  IContentService.cs
  INavigationAware.cs
Services/
  Region.cs
  PageService.cs
  ContentService.cs
  DialogService.cs
Common/
  RegionManager.cs
```

- [ ] **Step 2: Replace XAML region examples**

Use `Region.RegionName` and show both supported host types:

```xml
<Window
    xmlns:sn="clr-namespace:SimpleNavigation.Services;assembly=SimpleNavigation">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition />
            <RowDefinition />
        </Grid.RowDefinitions>

        <Frame
            Grid.Row="0"
            NavigationUIVisibility="Hidden"
            sn:Region.RegionName="Pages" />

        <ContentControl
            Grid.Row="1"
            sn:Region.RegionName="Content" />
    </Grid>
</Window>
```

State directly below the example that `PageService` requires a `Frame`, while `ContentService` requires a non-`Frame` `ContentControl`.

- [ ] **Step 3: Document DI and route registration without DataContext ownership**

Add this complete registration example:

```csharp
var services = new ServiceCollection();
services.RegisterNavigationService();

// Ordinary DI registration is enough for generic and Type navigation.
services.AddTransient<HomePage>();
services.AddSingleton<HomeViewModel>();

// Route helpers optionally register the view/view model and a string alias.
services.AddPage<SettingsPage>("settings");
services.AddPage<ReportsPage, ReportsViewModel>("reports");
services.AddContent<DashboardView, DashboardViewModel>();
services.AddContent<StatusView, StatusViewModel>("status");
```

Explain that the helpers use `TryAddTransient`, preserve registrations made earlier, and never set `DataContext`.

- [ ] **Step 4: Document all three navigation paths**

Add page examples:

```csharp
pageService.Navigate<HomePage>("Pages");
pageService.Navigate("Pages", typeof(HomePage));
pageService.Navigate("Pages", "settings");
pageService.GoBack("Pages");
```

Add content examples:

```csharp
contentService.Navigate<DashboardView>("Content");
contentService.Navigate("Content", typeof(DashboardView));
contentService.Navigate("Content", "status");
```

State that only the string overload reads the route dictionary; all three paths resolve the final type through ordinary DI.

- [ ] **Step 5: Document awareness and migration**

Use this awareness example:

```csharp
public sealed class StatusViewModel : INavigationAware
{
    public void OnNavigated(DialogParameters? parameters)
    {
        var id = parameters?.Get<int>("id");
    }
}
```

Add a breaking migration note with these exact mappings:

```text
RegionService.RegionName -> Region.RegionName
IPageService.GetRegion(...) -> IRegionManager.GetRegion(...)
IPageService.Goback(...) -> IPageService.GoBack(...)
```

State that `RegionService` is deleted, `Goback` remains only as an obsolete forwarding method, and compiled XAML/BAML must be rebuilt.

- [ ] **Step 6: Document future host extension limits**

State that `Grid` and `StackPanel` may be navigation targets today but are not region hosts. Explain that future panel and tab adapters require explicit child ownership, tab creation, selection, reuse, and header policies before those hosts can be enabled.

- [ ] **Step 7: Verify README names and examples**

Run:

```powershell
rg -n "RegionService\.RegionName|pageService\.GetRegion|\.Goback\(" README.md
rg -n "Region\.RegionName|AddPage|AddContent|IContentService|INavigationAware|IRegionManager" README.md
```

Expected: the first command finds only the intentional migration mapping, and the second command finds every rebuilt API section.

- [ ] **Step 8: Commit documentation**

```powershell
git add README.md
git commit -m "docs: document rebuilt navigation APIs"
```

### Task 8: Complete Cross-Target Verification And Review

**Files:**
- Modify only files required by failures or review findings from Tasks 1-7.

- [ ] **Step 1: Verify no production reference to RegionService remains**

Run:

```powershell
rg -n "RegionService" Common Extensions Interface Services SimpleNavigation.csproj
```

Expected: exit code 1 with no matches.

- [ ] **Step 2: Restore and build Debug once**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" restore SimpleNavigation.sln
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" build SimpleNavigation.sln -c Debug --no-restore --nologo
```

Expected: both library targets and both test targets build with zero errors. Investigate and remove warnings introduced by the rebuild.

- [ ] **Step 3: Run each target's complete test suite without rebuilding**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -c Debug -f net48 --no-build --nologo
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" test Tests\SimpleNavigation.Tests\SimpleNavigation.Tests.csproj -c Debug -f net8.0-windows --no-build --nologo
```

Expected: all tests pass on each target with zero failures.

- [ ] **Step 4: Build the complete Release solution and package**

Run:

```powershell
dotnet "C:\Program Files\dotnet\sdk\9.0.305\dotnet.dll" build SimpleNavigation.sln -c Release --no-restore --nologo
```

Expected: `net48` and `net8.0-windows` library outputs, both test assemblies, and the NuGet package build successfully with zero errors.

- [ ] **Step 5: Audit the public surface and worktree**

Run:

```powershell
rg -n "public (class|interface|static class)|public static IServiceCollection" Common Extensions Interface Services -g "*.cs"
git diff --check
git status --short --branch
```

Expected: public APIs match the approved spec, `git diff --check` is empty, and only intentional implementation changes remain.

- [ ] **Step 6: Request focused code review**

Dispatch a reviewer with the design spec, this plan, the pre-implementation commit, and current HEAD. Require findings on region ownership tokens, static subscription disposal, both route namespaces, DI lifetime preservation, dispatcher access, awareness ordering, and public API completeness.

Expected: no Critical or Important findings remain unresolved.

- [ ] **Step 7: Apply review fixes through TDD and re-run full verification**

For each valid finding, first add a focused failing test to the owning test file, run it to verify the expected failure, apply the smallest production fix, and rerun the focused plus complete suites from Steps 2-4.

- [ ] **Step 8: Commit review fixes when needed**

If review required changes, stage only those tests and production files and commit:

```powershell
git commit -m "fix: address navigation rebuild review"
```

If no files changed, do not create an empty commit.
