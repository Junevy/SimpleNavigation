# Introduction
An ultra-lightweight navigation framework that makes implementing navigation a breeze!
<br/><br/>
## Use
1) Using the SimpleNavigation.dll
2) Register and Build the DI container:
```C#
        public void InitialProvider()
        {
            var container = new ServiceCollection();

            container.AddTransient<MainWindow>();
            container.AddSingleton<MainWindowViewModel>();
            container.AddTransient<TestWindow>();
            container.AddSingleton<TestViewModel>();

            container.AddSingleton( (p) => new TestPage() { ShowsNavigationUI = false});

            container.AddSingleton<IDialogService, DialogService>();
            container.AddSingleton<IPageService, PageService>();

            container.AddSingleton<IServiceProvider, ServiceProvider>();
            Provider = container.BuildServiceProvider();
        }
```
3) Register Region and binding Command:
```C#
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition MaxHeight="20" />
            <RowDefinition />
        </Grid.RowDefinitions>

        <Menu Grid.Row="0">
            <MenuItem Header="Test">
                <MenuItem Header="Test">
                    <MenuItem Command="{Binding OpenWindowCommand}" Header="test1" />
                    <MenuItem Command="{Binding RegionTestCommand}" Header="test1" />
                </MenuItem>

                <MenuItem Header="demo">
                    <MenuItem Header="demo1" />
                </MenuItem>
            </MenuItem>
        </Menu>

        <Frame Grid.Row="1" sn:RegionService.RegionName="Main" />

    </Grid>
```
4) Navigate to other Page or show Dialog:
```C#
        [RelayCommand]
        public void OpenWindow()
        {
            var param = new DialogParameters("key", "value");
            var result = dialogService.ShowDialog<TestWindow>(param);
        }

        [RelayCommand]
        public void RegionTest()
        {
            pageService.Navigate<TestPage>("Main");
        }
```
5) The Result:
<img width="984" height="555" alt="image" src="https://github.com/user-attachments/assets/f1692e72-eace-44f8-a6c7-171243d2f854" />


