# SimpleNavigation

[![NuGet](https://img.shields.io/nuget/v/Junevy.SimpleNavigation.svg)](https://www.nuget.org/packages/Junevy.SimpleNavigation/)

一个超轻量级的 WPF 导航框架，基于 `Microsoft.Extensions.DependencyInjection` 构建，让 WPF 应用的页面导航和窗口管理变得简单直观。

---

## 项目介绍

SimpleNavigation 是一个受 Prism 启发的 WPF 导航库，但去除了 Prism 的复杂性，仅保留最核心的导航能力。整个框架仅包含 11 个 C# 源文件，零额外依赖（只依赖微软官方的 DI 容器），提供三大核心功能：

- **区域（Region）页面导航** — 在命名 `Frame` 区域内进行 `Page` 的导航与回退
- **窗口（Dialog）管理** — 打开 WPF `Window`，支持模态/非模态、参数传递与结果返回
- **导航参数传递** — 通过 `DialogParameters` 在页面和窗口之间传递强类型参数

配合 `CommunityToolkit.Mvvm` 使用效果更佳。

---

## 环境要求

| 目标框架 | 说明 |
|----------|------|
| `.NET 8.0` (net8.0-windows) | 现代 .NET WPF 应用 |
| `.NET Framework 4.8` (net48) | 传统 .NET Framework WPF 应用 |

开发环境：Visual Studio 2022 (v17.14+)，C# 13。

---

## 依赖信息

### NuGet 包

```
Junevy.SimpleNavigation
```

### 框架依赖

| 目标框架 | 依赖项 | 版本 |
|----------|--------|------|
| net8.0-windows | `Microsoft.Extensions.DependencyInjection` | 8.0.0 |
| net48 | `Microsoft.Extensions.DependencyInjection` | 6.0.1 |

仅依赖微软官方的 DI 容器，无需 Prism 或其他重型框架。

---

## 功能简介与演示

### 1. 项目结构

```
SimpleNavigation/
├── Common/
│   ├── DialogManager.cs        # 窗口生命周期管理，WeakReference 缓存防止内存泄漏
│   └── DialogParameters.cs     # 导航参数对象，支持字典/索引/键值三种构造
├── Extensions/
│   └── NavigationExtensions.cs # DI 容器注册扩展方法
├── Interface/
│   ├── IPageService.cs         # 页面导航服务接口
│   ├── IPageAware.cs           # 页面感知接口（接收导航事件）
│   ├── IDialogService.cs       # 窗口服务接口
│   ├── IDialogAware.cs         # 窗口感知接口（接收导航事件 + 请求关闭）
│   └── IDialogManager.cs       # 窗口实例管理接口
└── Services/
    ├── PageService.cs          # 页面导航实现
    ├── DialogService.cs        # 窗口管理实现
    └── RegionService.cs        # 区域附加属性（XAML 声明式注册）
```

### 2. 快速开始

#### 2.1 安装

通过 NuGet 安装：

```bash
dotnet add package Junevy.SimpleNavigation
```

或使用 Package Manager：

```
Install-Package Junevy.SimpleNavigation
```

#### 2.2 注册导航服务

在 `App.xaml.cs` 中使用一行代码完成所有服务注册：

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleNavigation.Extensions;

public partial class App : Application
{
    public IServiceProvider Provider { get; private set; }

    public void InitialProvider()
    {
        var container = new ServiceCollection();

        // 注册导航服务（一行注册 IDialogService / IPageService / IDialogManager）
        container.RegisterNavigationService();

        // 注册你的页面、窗口和 ViewModel
        container.AddTransient<MainWindow>();
        container.AddSingleton<MainWindowViewModel>();
        container.AddTransient<TestWindow>();
        container.AddSingleton<TestViewModel>();
        container.AddSingleton((p) => new TestPage() { ShowsNavigationUI = false });

        Provider = container.BuildServiceProvider();
    }
}
```

#### 2.3 注册导航区域（Region）

在 XAML 中通过附加属性声明导航区域：

```xml
<Window x:Class="SimpleNavigationDemo.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:sn="clr-namespace:SimpleNavigation.Services;assembly=SimpleNavigation">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition MaxHeight="20" />
            <RowDefinition />
        </Grid.RowDefinitions>

        <Menu Grid.Row="0">
            <MenuItem Header="Test">
                <MenuItem Command="{Binding OpenWindowCommand}" Header="打开窗口" />
                <MenuItem Command="{Binding RegionTestCommand}" Header="页面导航" />
            </MenuItem>
        </Menu>

        <!-- 声明一个名为 "Main" 的导航区域 -->
        <Frame Grid.Row="1" sn:RegionService.RegionName="Main" />
    </Grid>
</Window>
```

> `RegionService.RegionName` 只能附加到 `Frame` 控件上，否则会抛出异常。

### 3. 页面导航

#### 3.1 导航到指定页面

```csharp
using SimpleNavigation.Interface;
using SimpleNavigation.Common;

public class MainWindowViewModel
{
    private readonly IPageService pageService;

    public MainWindowViewModel(IPageService pageService)
    {
        this.pageService = pageService;
    }

    [RelayCommand]
    public void RegionTest()
    {
        // 泛型方式导航
        pageService.Navigate<TestPage>("Main");

        // 带参数导航
        var parameters = new DialogParameters("key", "value");
        pageService.Navigate<TestPage>("Main", parameters);

        // Type 方式导航
        pageService.Navigate("Main", typeof(TestPage), parameters);
    }

    [RelayCommand]
    public void GoBack()
    {
        // 返回上一页
        pageService.Goback("Main");
    }
}
```

#### 3.2 页面接收参数

让 ViewModel 实现 `IPageAware` 接口：

```csharp
using SimpleNavigation.Common;
using SimpleNavigation.Interface;

public class TestViewModel : IPageAware
{
    public event Action<DialogParameters?>? Receive;

    public void OnNavigated(DialogParameters? parameters)
    {
        if (parameters != null)
        {
            var value = parameters.Get<string>("key");
            // 处理导航参数
        }
    }
}
```

### 4. 窗口（Dialog）管理

#### 4.1 打开窗口

```csharp
using SimpleNavigation.Interface;
using SimpleNavigation.Common;

public class MainWindowViewModel
{
    private readonly IDialogService dialogService;

    public MainWindowViewModel(IDialogService dialogService)
    {
        this.dialogService = dialogService;
    }

    [RelayCommand]
    public void OpenWindow()
    {
        // 非模态打开窗口
        var param = new DialogParameters("key", "value");
        dialogService.Show<TestWindow>(param);
    }

    [RelayCommand]
    public void OpenModalDialog()
    {
        // 模态打开窗口，等待用户关闭并返回结果
        var param = new DialogParameters("key", "value");
        var result = dialogService.ShowDialog<TestWindow>(param);

        if (result != null)
        {
            var returnValue = result.Get<string>("resultKey");
        }
    }
}
```

#### 4.2 窗口接收参数并返回结果

让 ViewModel 或 Window 实现 `IDialogAware` 接口：

```csharp
using SimpleNavigation.Common;
using SimpleNavigation.Interface;

public class TestViewModel : IDialogAware
{
    public Action<DialogParameters?>? RequestClose { get; set; }

    public void OnNavigated(DialogParameters? parameters)
    {
        if (parameters != null)
        {
            var value = parameters.Get<string>("key");
            // 处理传入参数
        }
    }

    [RelayCommand]
    public void Close()
    {
        // 关闭窗口并返回结果
        var result = new DialogParameters("resultKey", "resultValue");
        RequestClose?.Invoke(result);
    }
}
```

> 框架支持 `IDialogAware` 同时实现在 Window 自身（code-behind）或 DataContext（ViewModel）上，两者可共存。

### 5. DialogParameters 参数对象

```csharp
// 方式一：键值对构造
var p1 = new DialogParameters("key", "value");

// 方式二：字典构造
var p2 = new DialogParameters(new Dictionary<string, object>
{
    { "id", 100 },
    { "name", "test" },
    { "data", someObject }
});

// 方式三：索引构造
var p3 = new DialogParameters("hello", 123, DateTime.Now);
var first  = p3.Get<string>("0");  // "hello"
var second = p3.Get<int>("1");     // 123

// 获取 / 设置参数
var value = p1.Get<string>("key");
p1.Set("key", "newValue"); // 允许覆盖已有值
```

### 6. 架构设计要点

- **DI 驱动**：所有 Page 和 Window 均由 DI 容器解析，支持构造函数注入
- **WeakReference 窗口缓存**：`DialogManager` 使用 `WeakReference<Window>` 防止内存泄漏
- **附加属性注册区域**：通过 `RegionService.RegionName` 在 XAML 中声明式注册导航区域
- **线程安全**：`PageService` 使用 `ConcurrentDictionary<string, Frame>` 存储区域
- **双绑定感知**：框架同时检查 Window/Page 自身及其 `DataContext` 是否实现了感知接口
- **多目标框架**：同时支持 .NET Framework 4.8 和 .NET 8.0 Windows

### 7. 效果预览

![preview](https://github.com/user-attachments/assets/f1692e72-eace-44f8-a6c7-171243d2f854)

---

## License

This project is licensed under the [MIT License](LICENSE).
