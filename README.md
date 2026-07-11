# SimpleNavigation

[![NuGet](https://img.shields.io/nuget/v/Junevy.SimpleNavigation.svg)](https://www.nuget.org/packages/Junevy.SimpleNavigation/)

SimpleNavigation 是一个基于 `Microsoft.Extensions.DependencyInjection` 的轻量级 WPF 导航类库，支持 .NET 8 WPF 与 .NET Framework 4.8。

## 功能概览

- `PageService`：在命名的 `Frame` 区域中导航 `Page`，支持返回上一页。
- `ContentService`：在命名的非 `Frame` `ContentControl` 区域中显示 `UserControl`、自定义 `ContentControl` 或其他非 `Page`、非 `Window` 的 `FrameworkElement`。
- `DialogService`：通过 DI 创建并显示 `Window`，支持模态/非模态窗口、参数传递与结果返回。
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
    IDialogService.cs       # Window 显示服务
    IDialogAware.cs         # Window 导航与关闭通知
    IDialogManager.cs       # Window 实例管理
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

var provider = services.BuildServiceProvider();
```

`AddPage` 与 `AddContent` 使用 `TryAddTransient` 注册 View 和可选的 ViewModel，不会覆盖在它们之前添加的注册。需要自定义生命周期或工厂时，应先使用标准 DI 方法注册对应服务。这些扩展方法不会创建或设置 View 的 `DataContext`。

双泛型无 key 的重载只注册 View 和 ViewModel；单泛型加 key 的重载注册 View 与路由别名；双泛型加 key 的重载同时注册 View、ViewModel 与路由别名。Page 与 Content 的 key 空间相互独立、区分大小写，同一空间内不能重复注册 key。

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

Window 及其依赖需要先注册到 DI：

```csharp
services.AddTransient<TestWindow>();
services.AddTransient<TestViewModel>();
```

使用 `IDialogService` 显示非模态或模态窗口：

```csharp
var input = new DialogParameters("id", 42);

dialogService.Show<TestWindow>(input);

DialogParameters? result = dialogService.ShowDialog<TestWindow>(input);
var saved = result?.Get<bool>("saved");
```

Window 或它的 `DataContext` 可以实现 `IDialogAware` 来接收参数和请求关闭：

```csharp
public sealed class TestViewModel : IDialogAware
{
    public Action<DialogParameters?>? RequestClose { get; set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        var id = parameters?.Get<int>("id");
    }

    public void Save()
    {
        RequestClose?.Invoke(new DialogParameters("saved", true));
    }
}
```

`DialogManager` 使用弱引用缓存同类型 Window，并在 Window 关闭后移除记录。

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

`IPageService` 与 `IContentService` 注册为 singleton，并通过创建它们的 `IServiceProvider` 解析导航目标。若它们由根容器创建，那么在启用 `ValidateScopes` 时，从根容器解析 scoped 服务是无效的；从根容器解析的 disposable transient 会由 Microsoft DI 保留到根容器释放。

类库不会为每次导航创建或释放 scope，因为已显示 View 的生命周期属于应用。需要 scoped 或 disposable View/ViewModel 对象图的应用，必须自行持有合适的 scope，并制定与界面显示周期匹配的释放策略。

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

## License

This project is licensed under the [MIT License](LICENSE).
