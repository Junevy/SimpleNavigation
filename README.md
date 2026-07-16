# SimpleNavigation

[![NuGet](https://img.shields.io/nuget/v/Junevy.SimpleNavigation.svg)](https://www.nuget.org/packages/Junevy.SimpleNavigation/)

SimpleNavigation 是一个基于 `Microsoft.Extensions.DependencyInjection` 的轻量级 WPF 导航类库，支持 .NET 8 WPF 与 .NET Framework 4.8。

## 功能概览

- `PageService`：在命名的 `Frame` 区域中导航 `Page`，支持返回上一页。
- `ContentService`：在命名的非 `Frame` `ContentControl` 区域中显示 `UserControl`、自定义 `ContentControl` 或其他非 `Page`、非 `Window` 的 `FrameworkElement`。
- `DialogService`：通过 DI 或独立的 Dialog 字符串路由显示、模态显示和关闭 `Window`，并支持参数与模态结果传递。
- `RegionManager`：统一管理 XAML 声明或代码注册的命名区域，并以弱引用保存区域宿主。

导航目标全部由应用的 DI 容器创建。类库不设置 `DataContext`，因此 View 与 ViewModel 的构造注入和绑定方式仍由应用决定。

## 环境要求

| 目标框架 | DI 依赖 |
| --- | --- |
| `net8.0-windows` | `Microsoft.Extensions.DependencyInjection` 8.0.0 |
| `net48` | `Microsoft.Extensions.DependencyInjection` 6.0.1 |

安装 NuGet 包：

```bash
dotnet add package Junevy.SimpleNavigation
```

## 项目结构

```text
SimpleNavigation/
  Interface/
    IRegionManager.cs       # 区域注册与查询
    IPageService.cs         # Page 导航
    IContentService.cs      # FrameworkElement 内容导航
    INavigationAware.cs     # 导航完成通知
    IPageAware.cs           # 兼容旧代码，继承 INavigationAware
    IDialogService.cs       # Window 显示、模态显示与关闭服务
    IDialogAware.cs         # Window 导航与关闭通知
    IDialogManager.cs       # Window 获取、复用与现有实例查询
  Services/
    Region.cs               # RegionName 附加属性
    PageService.cs          # Frame/Page 导航实现
    ContentService.cs       # ContentControl 内容导航实现
    DialogService.cs        # Window 显示实现
  Common/
    RegionManager.cs        # 命名区域管理
    DialogManager.cs        # Window 实例管理
    DialogParameters.cs     # 导航参数
  Extensions/
    NavigationExtensions.cs # DI 与路由注册扩展
```

## 注册服务和目标

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Extensions;

var services = new ServiceCollection();
services.RegisterNavigationService();

// 普通 DI 注册足以支持泛型导航和 Type 导航。
services.AddTransient<HomePage>();
services.AddSingleton<HomeViewModel>();

// 路由扩展可同时注册 View/ViewModel，并可选择添加字符串别名。
services.AddPage<SettingsPage>("settings");
services.AddPage<ProfilePage, ProfileViewModel>();
services.AddPage<ReportsPage, ReportsViewModel>("reports");
services.AddContent<HelpView>("help");
services.AddContent<DashboardView, DashboardViewModel>();
services.AddContent<StatusView, StatusViewModel>("status");
services.AddWindow<LoginWindow>("login");
services.AddWindow<SettingsWindow, SettingsViewModel>();
services.AddWindow<ReportsWindow, ReportsViewModel>("reports");

var provider = services.BuildServiceProvider();
```

`AddWindow` 使用 `TryAddTransient` 注册 Window 和可选的 ViewModel，不会覆盖在它之前添加的注册。需要自定义生命周期或工厂时，应先使用标准 DI 方法注册对应服务。它只负责注册，绝不会设置 Window 的 `DataContext`。

`AddPage` 与 `AddContent` 同样保留更早的注册，也不会创建或设置 View 的 `DataContext`。

双泛型无 key 的重载只注册 View 和 ViewModel；单泛型加 key 的重载注册 View 与路由别名；双泛型加 key 的重载同时注册 View、ViewModel 与路由别名。Page、Content 与 Dialog 的 key 空间相互独立，均采用 ordinal、区分大小写的比较；同一个 key 可以分别用于三种路由，但同一空间内不能重复注册。

## 声明导航区域

```xml
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
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

`PageService` 的区域宿主必须是 `Frame`。`ContentService` 当前要求区域宿主是非 `Frame` 的 `ContentControl`。也可以通过 `IRegionManager.RegisterRegion(...)` 显式注册宿主，并通过 `GetRegion(...)` 查询。

## Page 导航

`IPageService` 提供泛型、`Type` 和字符串 key 三种导航方式：

```csharp
using SimpleNavigation.Common;
using SimpleNavigation.Interface;

public sealed class MainViewModel
{
    private readonly IPageService pageService;

    public MainViewModel(IPageService pageService)
    {
        this.pageService = pageService;
    }

    public void OpenPages()
    {
        var parameters = new DialogParameters("id", 42);

        pageService.Navigate<HomePage>("Pages", parameters);
        pageService.Navigate("Pages", typeof(HomePage), parameters);
        pageService.Navigate("Pages", "settings", parameters);
    }

    public void Back()
    {
        pageService.GoBack("Pages");
    }
}
```

泛型重载直接从 DI 解析泛型类型，`Type` 重载直接从 DI 解析传入类型。只有字符串重载先从 Page 路由字典取得最终类型；取得类型后仍通过普通 DI 解析 Page 实例。

## Content 导航

`IContentService` 同样提供三种导航方式：

```csharp
using SimpleNavigation.Common;
using SimpleNavigation.Interface;

public sealed class ShellViewModel
{
    private readonly IContentService contentService;

    public ShellViewModel(IContentService contentService)
    {
        this.contentService = contentService;
    }

    public void OpenContent()
    {
        var parameters = new DialogParameters("id", 42);

        contentService.Navigate<DashboardView>("Content", parameters);
        contentService.Navigate("Content", typeof(DashboardView), parameters);
        contentService.Navigate("Content", "status", parameters);
    }
}
```

泛型与 `Type` 重载不读取路由表。只有字符串重载从 Content 路由字典取得最终类型，随后通过普通 DI 解析实例。导航目标可以是 `UserControl`、普通或自定义 `ContentControl`、`Grid`、`StackPanel` 等 `FrameworkElement`，但不能是 `Page` 或 `Window`。

## 导航通知

Page 或 Content 导航成功后，目标 View 及其 `DataContext` 中实现了 `INavigationAware` 的对象都会收到通知；如果二者是同一实例，则只通知一次。

```csharp
using SimpleNavigation.Common;
using SimpleNavigation.Interface;

public sealed class StatusViewModel : INavigationAware
{
    public void OnNavigated(DialogParameters? parameters)
    {
        var id = parameters?.Get<int>("id");
    }
}
```

类库只发送通知，不负责把 `StatusViewModel` 设置为 View 的 `DataContext`。

## DialogService

Window 及其依赖需要先注册到 DI；需要字符串 key 时再注册 Dialog 路由：

```csharp
services.AddWindow<LoginWindow>("login");
services.AddWindow<SettingsWindow, SettingsViewModel>();
services.AddWindow<ReportsWindow, ReportsViewModel>("reports");
```

`IDialogService` 的非模态显示、模态显示与关闭操作都提供泛型、`Type` 和字符串 key 三种形式：

```csharp
dialogService.Show<LoginWindow>();
dialogService.Show(typeof(LoginWindow));
dialogService.Show("login");

DialogParameters? genericResult = dialogService.ShowDialog<LoginWindow>();
DialogParameters? typeResult = dialogService.ShowDialog(typeof(LoginWindow));
DialogParameters? keyResult = dialogService.ShowDialog("login");

bool closedByGeneric = dialogService.Close<LoginWindow>();
bool closedByType = dialogService.Close(typeof(LoginWindow));
bool closedByKey = dialogService.Close("login");
```

`Show` 与 `ShowDialog` 的三种重载都可以额外接收 `DialogParameters`。

泛型与 `Type` 重载只需要普通 DI 注册，不读取路由表。字符串重载先在独立的 Dialog 路由空间中按 ordinal、区分大小写的 key 查找 Window 类型，随后仍通过普通 DI 解析实例；未知 key 会抛出 `KeyNotFoundException`。Page、Content 与 Dialog 可使用相同 key，互不冲突。

`AddWindow` 默认把 Window 注册为 transient，但 `DialogManager` 会在窗口仍然存活且未关闭时复用当前实例。因此，对同一 Window 类型重复 `Show` 会显示或激活当前实例；即使它当前没有焦点，`Close` 也能关闭它。窗口触发 `Closed` 后记录会移除，下一次 `Show` 才从 DI 创建新的 transient 实例。`Close` 只查询现有实例，绝不会为了关闭而创建窗口；没有受管理的现有实例时返回 `false`，`Closing` 被取消时也返回 `false`。

Window 以及它的、且与 Window 不同的 `DataContext` 都可以实现 `IDialogAware`，两者会按 Window、DataContext 的顺序接收参数和关闭回调。类库不会设置或替换 `DataContext`：

```csharp
public sealed class LoginViewModel : IDialogAware
{
    public Action<DialogParameters?>? RequestClose { get; set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        var id = parameters?.Get<int>("id");
    }

    public void Accept()
    {
        RequestClose?.Invoke(new DialogParameters("accepted", true));
    }
}
```

在模态显示中，`RequestClose(result)` 只有在 Window 实际完成关闭后才提交并由 `ShowDialog` 返回；如果关闭被取消，该候选结果不会提交。用户通过系统关闭按钮等方式直接关闭模态窗口时返回 `null`。非模态与模态操作会以事务方式安装、清理或在失败时恢复相关回调，且不会清除应用自行替换的回调。对同一 Window 的活动模态显示执行重入的 `Show`/`ShowDialog` 会被拒绝，以免覆盖当前模态事务。

Window 的显示、关闭以及 `RequestClose` 必须在其所属 Dispatcher 线程调用。

## DialogParameters

```csharp
var byKey = new DialogParameters("key", "value");

var byDictionary = new DialogParameters(new Dictionary<string, object>
{
    ["id"] = 100,
    ["name"] = "test",
});

var byIndex = new DialogParameters("hello", 123, DateTime.Now);
var first = byIndex.Get<string>("0");
var second = byIndex.Get<int>("1");

byKey.Set("key", "newValue");
var value = byKey.Get<string>("key");
```

## DI 生命周期

`RegisterNavigationService()` 默认把 `IRegionManager`、`IPageService`、`IContentService`、`IDialogService` 和 `IDialogManager` 注册为 singleton。`IRegionManager` 保存命名区域宿主的弱引用，并持有静态 `Region` 声明订阅；它不解析 View 对象图，并会在所属 DI provider 释放时取消订阅。

singleton 的 `PageService`、`ContentService` 和 `DialogManager` 会捕获创建它们的 `IServiceProvider`（通常是根容器）；仅创建或持有一个子 scope 不会把这些 singleton 的目标解析切换到该 scope。`DialogService` 构造时从 provider 取得并保存路由注册表，之后只持有该注册表与 `IDialogManager`，不会保留 provider 或直接解析 Window；Window 对象图由 `DialogManager` 从它捕获的 provider 解析。`AddWindow` 默认注册 transient Window 和 ViewModel，但从根 provider 解析的 disposable transient 或 scoped 对象图仍具有下述既有生命周期限制。

因此，在启用 `ValidateScopes` 时，从根容器解析 scoped View、ViewModel 或 Window 对象图是无效的；从根容器解析的 disposable transient 对象会由 Microsoft DI 保留到根容器释放。类库不会为导航或窗口创建、持有或释放 scope，因为已显示 UI 的生命周期属于应用。

需要 scoped 或 disposable View/ViewModel/Window 对象图的应用，必须在调用 `RegisterNavigationService()` 前覆盖相关导航服务和管理器的生命周期或解析策略（其 `TryAdd` 注册会保留先前注册），从应用持有的 scope 解析这些服务，并让 scope 的释放时机与对应 UI 生命周期一致。

## 区域宿主扩展边界

`Grid`、`StackPanel` 等元素现在可以作为 Content 导航目标，但不能作为区域宿主；`TabControl` 也尚未作为区域宿主启用。

内部区域适配器以 `FrameworkElement` 和宿主能力为边界，因此未来可以在 resolver 中增加适配器，而不必修改 `ContentService`。不过，在支持 `Panel` 前必须明确区域是否拥有全部子元素以及替换/追加策略；在支持 `TabControl` 前必须明确标签创建、选择、复用和 header 策略。

## 破坏性升级迁移

本次重建采用以下 API 映射：

```text
RegionService.RegionName -> Region.RegionName
IPageService.GetRegion(...) -> IRegionManager.GetRegion(...)
IPageService.Goback(...) -> IPageService.GoBack(...)
```

`RegionService` 已删除。`Goback` 仅作为标记了 `Obsolete` 的转发方法保留，现有代码应迁移到 `GoBack`。由于附加属性所有者发生变化，引用旧属性的已编译 XAML/BAML 必须重新构建。

Dialog API 也有破坏性变化：`IDialogService` 新增了 `Type`、字符串 key 与 `Close` 成员；自定义 `IDialogService` 实现必须补齐这些成员。自定义 `IDialogManager` 实现必须新增 `GetOrCreateWindow(Type)` 与 `GetExistingWindow(Type)`。直接构造 `DialogService` 的代码现在必须同时传入 `IServiceProvider` 和 `IDialogManager`。因此包含这些变更的 NuGet 包应作为破坏性的 2.x 版本升级处理。

## License

This project is licensed under the [MIT License](LICENSE).
