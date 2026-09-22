using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PTBox.Launcher;
using PTBox.Launcher.Services;
using PTBox.Launcher.ViewModels;
using PTBox.Launcher.Views;

namespace PTBox.Tests;

internal static class UiSmoke
{
    public static void Run(string output, string root, string fixture)
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var config = new ConfigService(root, new(root));
        var model = Program.LegacyConfig(); model.StartFullscreen = false; config.Save(model);
        var window = new MainWindow(config, new(root), root, backgroundUpdates: false);
        window.Show(); Pump();
        var vm = (MainViewModel)window.DataContext;
        Assert(vm.Selected.Id == "playnite", "首页初始选择");
        Key(window, System.Windows.Input.Key.Right); Pump(); Assert(vm.Selected.Id == "moonlight", "右方向键");
        Key(window, System.Windows.Input.Key.Down); Pump(); Assert(vm.Selected.Id == "$quick-app-playnite", "下方向键进入快捷入口");
        Key(window, System.Windows.Input.Key.Escape); Assert(window.IsVisible && vm.Selected.Id == "$quick-app-playnite", "首页返回不能退出");
        Key(window, System.Windows.Input.Key.Back); Key(window, System.Windows.Input.Key.BrowserBack);
        Assert(window.IsVisible, "遥控器返回不能退出");
        var desktop = Descendants(window).OfType<Button>().Single(b => b.DataContext is TileViewModel { Id: "$desktop" });
        desktop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Assert(window.WindowState == WindowState.Minimized, "桌面入口应最小化");
        window.ReturnHome(); Pump(); Assert(vm.Selected.Id == "$desktop" && window.WindowState == WindowState.Normal, "回首页恢复选择");
        vm.SelectById("$tab-games"); Key(window, System.Windows.Input.Key.Enter); Pump();
        Assert(vm.ActiveCategory == "games" && vm.Cards.Count == 2, "分类应真实筛选应用");
        Key(window, System.Windows.Input.Key.Escape); Pump(); Assert(vm.ActiveCategory == "home", "返回键回到首页分类");
        vm.SelectById("$slide-1"); Key(window,System.Windows.Input.Key.Enter); Pump(); Assert(vm.HeroIndex==1,"推荐页按钮切换");
        vm.SelectById("$slide-0"); Key(window,System.Windows.Input.Key.Enter); Pump();
        var expanded = model.Copy();
        for(var i=0;i<12;i++) expanded.Apps.Add(new() { Id="overflow-"+i, Name="扩展应用 "+i });
        vm.Refresh(expanded,root); vm.SelectById("playnite"); Pump();
        for(var i=1;i<expanded.Apps.Count;i++) { Key(window,System.Windows.Input.Key.Right); Pump(); }
        Assert(vm.Selected.Id=="overflow-11","横向列表末项可到达");
        var rail = (ScrollViewer)window.FindName("CardScroll");
        Assert(rail.HorizontalOffset>0,"键盘焦点应带动横向滚动");
        var focusedCard = Descendants(window).OfType<Button>().Single(x=>ReferenceEquals(x.DataContext,vm.Selected));
        var cardBounds = focusedCard.TransformToAncestor(rail).TransformBounds(new Rect(focusedCard.RenderSize));
        Assert(cardBounds.Left>=-1 && cardBounds.Right<=rail.ActualWidth+1,"末项焦点卡片须完整可见");
        model.LastSelectedTile="playnite"; vm.Refresh(model,root); Pump();
        vm.SelectById("playnite"); vm.Status=""; Pump();
        rail.ScrollToHorizontalOffset(0); Pump();
        var content = (FrameworkElement)window.Content;
        content.DataContext = vm;
        window.Content = null;
        Pump(); window.UpdateLayout();
        TextElementProperties(content, window);
        var renderHost = new Border { Resources = window.Resources, Child = content };
        foreach (var (width, height) in new[] { (1920,1080), (2560,1440), (3840,2160) })
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            Layout(content, width / scale, height / scale);
            var buttons = Descendants(content).OfType<Button>().Where(b => b.DataContext is TileViewModel).ToList();
            Assert(buttons.Count == 26, "首页各区按钮数量");
            foreach (var button in buttons)
            {
                // The app rail intentionally clips offscreen cards, including the trailing Add tile.
                if (button.DataContext is TileViewModel card && vm.AppTiles.Contains(card)) continue;
                var bounds = button.TransformToAncestor(content).TransformBounds(new Rect(button.RenderSize));
                Assert(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= width / scale + 1 && bounds.Bottom <= height / scale + 1, $"按钮越界: {width}x{height} @{scale}: {bounds}, root={content.RenderSize}, desired={content.DesiredSize}");
            }
            if (scale is 1.0 or 2.0 && width != 2560)
                Render(content, Path.Combine(output, $"home-{width}x{height}-{scale*100:0}pct.png"), width, height, scale);
        }
        content.Width = double.NaN; content.Height = double.NaN;
        renderHost.Child = null;
        window.Content = content; window.UpdateLayout();
        var minimal = ConfigService.DefaultConfig(); minimal.StartFullscreen=false;
        minimal.Apps=[AppTemplateService.Create("steam"),AppTemplateService.Create("moonlight")];
        vm.Refresh(minimal,root); Pump();
        RenderWindowContent(window,Path.Combine(output,"home-steam-moonlight.png"),1600,900);
        vm.Refresh(ConfigService.DefaultConfig(),root); Pump();
        RenderWindowContent(window,Path.Combine(output,"home-empty.png"),1600,900);
        Exception? addError=null;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
        {
            var picker=app.Windows.OfType<AddAppWindow>().SingleOrDefault();
            var editor=app.Windows.OfType<SettingsWindow>().SingleOrDefault();
            try
            {
                Assert(picker != null && editor != null,"首页添加入口必须打开应用选择器");
                RenderWindowContent(picker!,Path.Combine(output,"add-app.png"),660,700);
                ((Button)picker!.FindName("WebsiteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
                {
                    try
                    {
                        var draft=(SettingsViewModel)editor!.DataContext;
                        Assert(draft.Selected?.Type=="url","网页选择应建立网页草稿");
                        draft.Selected!.Name="我的网页";
                        var address=Descendants(editor).OfType<TextBox>().Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="程序路径");
                        address.Focus(); address.SetCurrentValue(TextBox.TextProperty,"https://www.baidu.com");
                        ((ComboBox)editor.FindName("TypePicker")).Focus(); Pump();
                        Assert(draft.Selected.Path=="https://www.baidu.com","网址输入框必须即时写入当前草稿");
                        draft.Draft.AutoStart=new AutoStartService().IsEnabled();
                        draft.Draft.StartFullscreen=false;
                        ((Button)editor.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(editor.Result != null,"保存新应用必须成功");
                    }
                    catch(Exception ex) { addError=ex; editor!.Close(); }
                }));
            }
            catch(Exception ex) { addError=ex; picker?.Close(); editor?.Close(); }
        }));
        vm.SelectById("$add-app"); Key(window,System.Windows.Input.Key.Enter); Pump();
        if(addError != null) throw addError;
        Assert(vm.Cards.Count==1 && vm.HeroSubtitle.Contains("我的网页"),"保存后首页应立即显示添加的应用");
        Assert(config.Load().Apps.Single().Name=="我的网页","新增应用应持久化");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
        {
            var editor=app.Windows.OfType<SettingsWindow>().Single();
            try
            {
                var draft=(SettingsViewModel)editor.DataContext;
                while(draft.Apps.Count > 0) { draft.Selected=draft.Apps[0]; draft.Remove(); }
                ((Button)editor.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(editor.Result != null,"删除全部应用后仍应允许保存");
            }
            catch(Exception ex) { addError=ex; editor.Close(); }
        }));
        vm.SelectById("$settings"); Key(window,System.Windows.Input.Key.Enter); Pump();
        if(addError != null) throw addError;
        Assert(vm.IsEmpty && vm.HeroPages.Count==0 && config.Load().Apps.Count==0,"保存删除后首页和磁盘都应为空，不能复活应用");
        var duplicatePicker=new AddAppWindow(minimal.Apps) { Owner=window };
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
        {
            try
            {
                Assert(!((Button)duplicatePicker.FindName("SteamButton")).IsEnabled && !((Button)duplicatePicker.FindName("MoonlightButton")).IsEnabled,"已添加模板应禁止重复添加");
                Key(duplicatePicker,System.Windows.Input.Key.Escape);
            }
            catch(Exception ex) { addError=ex; duplicatePicker.Close(); }
        }));
        Assert(duplicatePicker.ShowDialog()==false && duplicatePicker.Result==null,"取消添加不产生新应用");
        if(addError != null) throw addError;
        vm.Refresh(model,root); Pump();
        var settings = new SettingsWindow(model, config, new AutoStartService(), root, "moonlight") { Owner = window };
        settings.Show(); Pump();
        var settingsVm = (SettingsViewModel)settings.DataContext;
        Assert(settingsVm.Selected?.Id == "moonlight", "设置应定位当前应用");
        Key(settings, System.Windows.Input.Key.Right); Pump();
        Assert(Keyboard.FocusedElement is not ListBoxItem, "应用列表应能向右进入编辑区");
        var category = (ComboBox)settings.FindName("CategoryPicker");
        category.Focus(); Key(settings, System.Windows.Input.Key.Enter); Pump();
        Assert(category.IsDropDownOpen, "确认键展开设置下拉菜单");
        ((ComboBoxItem)category.ItemContainerGenerator.ContainerFromIndex(0)).Focus();
        Key(settings, System.Windows.Input.Key.Escape); Pump();
        Assert(!category.IsDropDownOpen && settings.IsVisible, "返回键应先关闭下拉菜单");
        category.SelectedValue = "streaming"; Pump();
        Assert(settingsVm.Selected!.Category == "streaming", "分类下拉菜单应更新草稿");
        Assert(model.Apps.Single(x => x.Id == "moonlight").Category != "streaming", "未保存设置不能修改原配置");
        RenderWindowContent(settings, Path.Combine(output, "settings.png"), 1600, 900);
        ((Button)settings.FindName("SystemTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Assert(settingsVm.IsSystemPage && ((Grid)settings.FindName("SystemPanel")).IsVisible, "系统设置页切换");
        var fullscreen = (CheckBox)settings.FindName("FullscreenToggle");
        fullscreen.Focus(); Key(settings, System.Windows.Input.Key.Enter); Pump();
        Assert(settingsVm.Draft.StartFullscreen && !model.StartFullscreen, "确认键切换开关且只修改草稿");
        RenderWindowContent(settings, Path.Combine(output, "settings-system.png"), 1600, 900);
        ((Button)settings.FindName("UpdatesTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Assert(settingsVm.IsUpdatePage && !settingsVm.IsSystemPage && !settingsVm.IsAppsPage, "更新页应独立显示");
        var updates = (UpdateService)((Grid)settings.FindName("UpdatePanel")).DataContext;
        Assert(updates.RepositoryUrl == "https://github.com/NeuronXL/PTBox/releases" && !updates.CanInstall, "更新源固定，未下载不允许安装");
        ((Button)settings.FindName("CheckUpdateButton")).Focus(); Key(settings, System.Windows.Input.Key.Down); Pump();
        Assert(Keyboard.FocusedElement is Button, "更新页方向键可切换按钮");
        RenderWindowContent(settings, Path.Combine(output, "settings-updates.png"), 1600, 900);
        ((Button)settings.FindName("AppsTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Assert(settingsVm.Selected?.Id == "moonlight" && settingsVm.Selected.Category == "streaming", "切换设置页应保留应用草稿");
        settings.Close();
        using (var previewUpdates = UpdateTests.PreviewService(root))
        {
            var preview = new SettingsWindow(model, config, new AutoStartService(), root, null, updates: previewUpdates) { Owner = window };
            preview.Show(); ((Button)preview.FindName("UpdatesTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            var checking = previewUpdates.CheckAsync();
            while (!checking.IsCompleted) { Pump(); Thread.Sleep(10); }
            checking.GetAwaiter().GetResult(); Pump(); Assert(previewUpdates.CanDownload && previewUpdates.Latest == "1.2.0", "模拟正式更新可下载");
            RenderWindowContent(preview, Path.Combine(output, "settings-update-available.png"), 1600, 900);
            var downloading = previewUpdates.DownloadAsync();
            while (!downloading.IsCompleted) { Pump(); Thread.Sleep(10); }
            downloading.GetAwaiter().GetResult(); Pump();
            Assert(!previewUpdates.CanDownload && previewUpdates.Status.Contains("校验完成") && !previewUpdates.CanInstall, "便携测试可下载，不能执行安装");
            RenderWindowContent(preview, Path.Combine(output, "settings-update-ready-portable.png"), 1600, 900); preview.Close();
        }
        CheckAddressValidation(window,output,root);
        var updaterWindow = new PTBox.Updater.UpdaterWindow(); updaterWindow.Show(); Pump();
        updaterWindow.Close(); Assert(updaterWindow.IsVisible, "安装阶段不能被误关闭");
        updaterWindow.ShowFailure("安装程序未完成。请保留配置，用官方安装包重新安装修复。\n\n日志：C:\\Users\\用户\\AppData\\Local\\PTBox\\Updates\\jobs\\example\\setup.log"); Pump();
        Assert(!updaterWindow.IsUpdating && Descendants(updaterWindow).OfType<Button>().Single().IsVisible, "失败恢复入口可见");
        RenderWindowContent(updaterWindow, Path.Combine(output, "updater-recovery.png"), 740, 440);
        updaterWindow.Close(); Assert(!updaterWindow.IsVisible, "失败后可以关闭更新器");
        CheckFilePickers(app,window,output,root);
        var menu = new PowerMenuWindow { Owner=window };
        Exception? menuError=null;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
        {
            try
            {
                Assert(Descendants(menu).OfType<Button>().Single(x=>x.Name=="CancelButton").IsKeyboardFocused,"电源菜单默认返回");
                RenderWindowContent(menu,Path.Combine(output,"power-menu.png"),530,560);
                ((Button)menu.FindName("CancelButton")).Focus(); // Rendering reparents the content and clears native focus.
                Key(menu,System.Windows.Input.Key.Down); Assert(Descendants(menu).OfType<Button>().Single(x=>x.Name=="ShutdownButton").IsKeyboardFocused,"电源菜单上下导航");
                Key(menu,System.Windows.Input.Key.Escape);
            }
            catch(Exception ex) { menuError=ex; menu.Close(); }
        }));
        Assert(menu.ShowDialog()==false && menu.Action==PowerMenuAction.None,"返回应取消电源菜单");
        if(menuError!=null) throw menuError;
        var power = new PowerDialog("今天就到这里？", "确认后将正常关闭 Windows，请先保存其他应用中的工作。", "关机") { Owner = window };
        Exception? dialogError = null;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try
            {
            Assert(Descendants(power).OfType<Button>().Single(x => x.Name == "CancelButton").IsKeyboardFocused, "电源确认必须默认取消");
            RenderWindowContent(power, Path.Combine(output, "power-confirmation.png"), 800, 410);
            Key(power, System.Windows.Input.Key.Right); Assert(Descendants(power).OfType<Button>().Single(x => x.Name == "ConfirmButton").IsKeyboardFocused, "电源方向键右移");
            Key(power, System.Windows.Input.Key.Left); Key(power, System.Windows.Input.Key.Escape);
            }
            catch (Exception ex) { dialogError = ex; power.Close(); }
        }));
        Assert(power.ShowDialog() == false, "取消不应确认电源操作");
        if (dialogError != null) throw dialogError;
        vm.SelectById("playnite"); vm.Selected.Item.Path = Path.GetFullPath(fixture); vm.Selected.Item.Arguments = "--lifetime 800";
        Key(window, System.Windows.Input.Key.Enter);
        PumpUntil(() => vm.Status.Contains("已退出"), TimeSpan.FromSeconds(10));
        Assert(vm.Selected.Id == "playnite" && window.IsVisible && window.WindowState == WindowState.Normal, "外部程序退出后应回到原卡片");
        window.Close();
        Console.WriteLine("UI render artifacts: " + output);
    }
    private static void CheckAddressValidation(Window owner,string output,string root)
    {
        var service=new ConfigService(Path.Combine(root,"address-validation"),new(root));
        var model=ConfigService.DefaultConfig(); model.AutoStart=new AutoStartService().IsEnabled();
        model.Apps=[new() { Id="valid",Name="有效网页",Type="url",Path="https://www.baidu.com" },new() { Id="invalid",Name="待修复网页",Type="url",Path="" }];
        service.Save(model); var original=File.ReadAllText(service.ConfigPath);
        var editor=new SettingsWindow(model,service,new(),root,"valid") { Owner=owner };
        Exception? error=null;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
        {
            try
            {
                var save=(Button)editor.FindName("SaveButton"); save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var draft=(SettingsViewModel)editor.DataContext;
                Assert(editor.Result==null && editor.IsVisible && draft.Selected?.Id=="invalid","保存失败必须定位另一条空网址应用");
                Assert(((TextBlock)editor.FindName("Status")).Text.Contains("待修复网页"),"错误必须指出应用名称");
                Assert(File.ReadAllText(service.ConfigPath)==original,"校验失败不能写入配置");
                var path=(TextBox)editor.FindName("AppPath");
                path.SetCurrentValue(TextBox.TextProperty,"www.baidu.com"); save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(editor.Result==null && path.IsKeyboardFocused,"缺少协议时保持窗口并聚焦网址");
                RenderWindowContent(editor,Path.Combine(output,"settings-address-validation.png"),1600,900);
                Assert(Descendants(editor).OfType<Button>().Single(x=>Equals(x.Content,"浏览…")).Visibility==Visibility.Collapsed,"网页模式隐藏 EXE 浏览入口");
                path.Focus(); path.SetCurrentValue(TextBox.TextProperty,"https：／／www.baidu.com");
                Assert(((TextBlock)editor.FindName("Status")).Text=="","重新编辑地址应清除旧保存错误");
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch(Exception ex) { error=ex; editor.Close(); }
        }));
        var saved=editor.ShowDialog(); if(error!=null) throw error;
        Assert(saved==true && service.Load().Apps.All(x=>x.Path=="https://www.baidu.com"),"修正网址后保存成功且全角协议分隔符规范化");
    }
    private static void CheckFilePickers(App app,Window owner,string output,string root)
    {
        Exception? error=null;
        var add=new AddAppWindow([]) { Owner=owner };
        var folder=Path.Combine(root,"picker");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
            {
                var picker=app.Windows.OfType<FilePickerWindow>().Single();
                try
                {
                    var vm=(FilePickerViewModel)picker.DataContext;
                    var loading=vm.NavigateAsync(folder); PumpUntil(()=>loading.IsCompleted,TimeSpan.FromSeconds(5)); loading.GetAwaiter().GetResult();
                    var list=(ListBox)picker.FindName("FileList");
                    list.SelectedItem=vm.Entries.Single(x=>x.Name=="程序.EXE"); Pump();
                    RenderWindowContent(picker,Path.Combine(output,"file-picker.png"),1140,760);
                    var path=(TextBox)picker.FindName("FilePath"); path.Text=Path.Combine(folder,"说明.txt");
                    ((Button)picker.FindName("SelectButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(picker.IsVisible && picker.SelectedPath==null && vm.Status.Contains("类型"),"非法类型不能关闭文件选择器");
                    path.Text=Path.Combine(folder,"程序.EXE"); path.Focus(); Key(picker,System.Windows.Input.Key.Enter);
                }
                catch(Exception ex) { error=ex; picker.Close(); }
            }));
            ((Button)add.FindName("LocalButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(error!=null) add.Close();
        }));
        var result=add.ShowDialog(); if(error!=null) throw error;
        Assert(result==true && add.Result?.Path==Path.Combine(folder,"程序.EXE") && add.Result.Name=="程序","添加本地程序应接收文件选择结果");

        foreach (var name in new[] { "桌面程序.LNK", "游戏.URL" })
        {
            var shortcutAdd = new AddAppWindow([]) { Owner=owner };
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
                {
                    var picker=app.Windows.OfType<FilePickerWindow>().Single();
                    try
                    {
                        var vm=(FilePickerViewModel)picker.DataContext;
                        var loading=vm.NavigateAsync(folder); PumpUntil(()=>loading.IsCompleted,TimeSpan.FromSeconds(5)); loading.GetAwaiter().GetResult();
                        Assert(vm.Entries.Any(x=>x.Name==name),"文件列表应显示桌面快捷方式");
                        var path=(TextBox)picker.FindName("FilePath");
                        var invalid=Path.Combine(folder,"invalid.URL"); File.WriteAllText(invalid,"[InternetShortcut]\nURL=javascript:alert(1)");
                        path.Text=invalid; ((Button)picker.FindName("SelectButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(picker.IsVisible && picker.SelectedApp==null && vm.Status.Contains("不支持"),"无效快捷方式应留在选择器并显示错误");
                        path.Text=Path.Combine(folder,name); path.Focus(); Key(picker,System.Windows.Input.Key.Enter);
                    }
                    catch(Exception ex) { error=ex; picker.Close(); }
                }));
                ((Button)shortcutAdd.FindName("LocalButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(error!=null) shortcutAdd.Close();
            }));
            var accepted=shortcutAdd.ShowDialog(); if(error!=null) throw error;
            Assert(accepted==true && shortcutAdd.Result?.Name==Path.GetFileNameWithoutExtension(name),"快捷方式名称应进入添加草稿");
            Assert(shortcutAdd.Result!.Path==(name.EndsWith(".LNK") ? Path.Combine(folder,name) : "steam://rungameid/123"),"正确保存快捷方式路径或游戏协议");
        }

        var images=new FilePickerWindow(true,folder) { Owner=owner };
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
        {
            try
            {
                var vm=(FilePickerViewModel)images.DataContext; PumpUntil(()=>!vm.IsBusy,TimeSpan.FromSeconds(5));
                Assert(vm.Entries.Any(x=>x.Name=="背景.png") && !vm.Entries.Any(x=>x.Name=="程序.EXE"),"图片选择器只显示图片与目录");
                var drives=(ComboBox)images.FindName("Drives"); drives.Focus(); Key(images,System.Windows.Input.Key.Enter); Pump();
                Assert(drives.IsDropDownOpen,"文件选择器磁盘菜单可通过确认键展开");
                Key(images,System.Windows.Input.Key.Escape); Assert(images.IsVisible && !drives.IsDropDownOpen,"首次返回只关闭下拉框");
                ((Button)images.FindName("CancelButton")).Focus(); Key(images,System.Windows.Input.Key.Back);
            }
            catch(Exception ex) { error=ex; images.Close(); }
        }));
        var canceled=images.ShowDialog(); if(error!=null) throw error;
        Assert(canceled==false && images.SelectedPath==null,"返回取消图片选择，不产生修改");
    }
    private static void RenderWindowContent(Window window, string file, int width, int height)
    {
        var content = (FrameworkElement)window.Content; content.DataContext = window.DataContext; window.Content = null; Pump(); window.UpdateLayout();
        TextElementProperties(content, window);
        var renderHost = new Border { Resources = window.Resources, Child = content };
        Layout(content, width, height); Render(content, file, width, height, 1);
        content.Width = double.NaN; content.Height = double.NaN;
        renderHost.Child = null;
        window.Content = content; Pump();
    }
    private static void Layout(FrameworkElement content, double width, double height)
    { content.Width = width; content.Height = height; content.Measure(new Size(width,height)); content.Arrange(new Rect(0,0,width,height)); content.UpdateLayout(); }
    private static void TextElementProperties(FrameworkElement content, Window window)
    {
        System.Windows.Documents.TextElement.SetForeground(content, window.Foreground);
        System.Windows.Documents.TextElement.SetFontFamily(content, window.FontFamily);
        if (content is Panel panel) panel.Background = window.Background;
    }
    private static void Render(Visual visual, string file, int width, int height, double scale)
    {
        var bitmap = new RenderTargetBitmap(width,height,96*scale,96*scale,PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        { var child=VisualTreeHelper.GetChild(parent,i); yield return child; foreach(var nested in Descendants(child)) yield return nested; }
    }
    private static void Key(Window window, Key key) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
    }
    private static void PumpUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var frame = new DispatcherFrame(); var clock = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) => { if (predicate() || clock.Elapsed > timeout) frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); Assert(predicate(), "等待 UI 生命周期恢复超时");
    }
    private static void Assert(bool value, string message) { if(!value) throw new InvalidOperationException(message); }
}
