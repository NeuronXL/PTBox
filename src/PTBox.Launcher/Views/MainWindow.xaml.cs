using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PTBox.Launcher.Services;
using PTBox.Launcher.ViewModels;
using PTBox.UpdateCore;
using System.IO;

namespace PTBox.Launcher.Views;

public partial class MainWindow : Window
{
    private readonly ConfigService _config;
    private readonly LoggingService _log;
    private readonly string _dataDirectory;
    private readonly AppLaunchService _launch;
    private readonly AutoStartService _autoStart = new();
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(15) };
    private CancellationTokenSource? _watch;
    private HwndSource? _source;
    private bool _launching;
    private bool _closed;
    private bool _hotkeyRegistered;
    private bool _fullscreen;
    private Window? _dialog;
    private readonly UpdateService _updates;
    private bool _updating;
    public MainWindow(ConfigService config, LoggingService log, string dataDirectory, bool backgroundUpdates = true)
    {
        _config = config; _log = log; _dataDirectory = dataDirectory;
        _updates = new UpdateService(dataDirectory);
        _launch = new(dataDirectory, log);
        _log.Info("初始化首页视图");
        InitializeComponent();
        _log.Info("读取首页配置");
        DataContext = _vm;
        _vm.Refresh(config.Load(), dataDirectory);
        _log.Info("首页配置加载完成");
        // Read registry state; loading config never silently changes Windows startup settings.
        _vm.Config.AutoStart = _autoStart.IsEnabled();
        _fullscreen = _vm.Config.StartFullscreen;
        SetFullscreen(_fullscreen);
        _clock.Tick += (_, _) => _vm.Tick(); _clock.Start();
        Loaded += (_, _) =>
        {
            if (_fullscreen) WindowService.FullscreenPrimary(this);
            FocusSelected();
            if (config.LoadWarning != null) _vm.Status = config.LoadWarning;
            if (backgroundUpdates) _ = _updates.BackgroundCheckAsync();
        };
        _updates.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(UpdateService.CanDownload) && _updates.CanDownload) _vm.Status = "PTBox 有新版本 · 设置 → 关于与更新"; };
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source.AddHook(WindowMessage);
        _hotkeyRegistered = WindowService.RegisterHotKey(_source.Handle, 1, 0x0002 | 0x0001 | 0x4000, 0x48); // Ctrl+Alt+H, no repeat
        if (!_hotkeyRegistered) { _vm.Status = "Ctrl+Alt+H 被占用；再次运行 PTBox 也可返回首页。"; _log.Info("返回热键注册失败"); }
    }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == 1) { ReturnHome(); handled = true; }
        if (message is 0x007E or 0x02E0 && _fullscreen) Dispatcher.BeginInvoke(() => WindowService.FullscreenPrimary(this));
        return IntPtr.Zero;
    }
    public void ReturnHome()
    {
        if (_closed) return;
        WindowService.Restore(this, _fullscreen);
        if (_fullscreen) WindowService.FullscreenPrimary(this);
        if (_dialog != null) { _dialog.Activate(); return; }
        FocusSelected();
    }
    private void SetFullscreen(bool enabled)
    {
        _fullscreen = enabled;
        WindowState = WindowState.Normal;
        WindowStyle = enabled ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        ResizeMode = enabled ? ResizeMode.NoResize : ResizeMode.CanResize;
        WindowState = enabled ? WindowState.Maximized : WindowState.Normal;
        if (enabled && IsLoaded) WindowService.FullscreenPrimary(this);
    }
    private void OnActivated(object? sender, EventArgs e) { if (IsLoaded && _dialog == null) FocusSelected(); }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return; // Alt+F4 and Windows shortcuts remain available.
        var direction = e.Key switch { Key.Left => NavigationDirection.Left, Key.Right => NavigationDirection.Right, Key.Up => NavigationDirection.Up, Key.Down => NavigationDirection.Down, _ => (NavigationDirection?)null };
        if (direction.HasValue) { _vm.Move(direction.Value); FocusSelected(); e.Handled = true; }
        else if (e.Key is Key.Enter or Key.Space) { e.Handled = true; if (!e.IsRepeat) _ = ActivateSelectedAsync(); }
        else if (e.Key is Key.Escape or Key.Back or Key.BrowserBack) { e.Handled = true; _vm.Back(); FocusSelected(); }
        else if (e.Key == Key.F11) { e.Handled = true; SetFullscreen(!_fullscreen); }
    }
    private void OnTileFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is Button { DataContext: TileViewModel tile }) _vm.Select(tile);
    }
    private async void OnTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TileViewModel tile }) return;
        _vm.Select(tile); await ActivateSelectedAsync();
    }
    private void OnCardWheel(object sender, MouseWheelEventArgs e)
    {
        CardScroll.ScrollToHorizontalOffset(CardScroll.HorizontalOffset - e.Delta * 2);
        e.Handled = true;
    }
    private void FocusSelected()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_closed || _dialog != null || !IsActive) return;
            var button = FindSelectedButton(this, _vm.Selected);
            button?.BringIntoView(); button?.Focus();
        }));
    }
    private static Button? FindSelectedButton(DependencyObject parent, TileViewModel selected)
    {
        if (parent is Button button && ReferenceEquals(button.DataContext, selected)) return button;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var found = FindSelectedButton(VisualTreeHelper.GetChild(parent, i), selected); if (found != null) return found; }
        return null;
    }
    private async Task ActivateSelectedAsync()
    {
        if (_launching) return;
        var tile = _vm.Selected;
        if (tile.Id.StartsWith("$tab-")) { _vm.SetCategory(tile.Id[5..]); FocusSelected(); return; }
        if (tile.Id.StartsWith("$slide-") && int.TryParse(tile.Id[7..], out var slide)) { _vm.SetHero(slide); FocusSelected(); return; }
        switch (tile.Id)
        {
            case "$add-app":
            case "$quick-add": OpenSettings(addApp:true); return;
            case "$hero-launch" when _vm.Config.Apps.Count == 0: OpenSettings(addApp:true); return;
            case "$top-settings":
            case "$settings": OpenSettings(); return;
            case "$quick-desktop":
            case "$desktop": SaveSelection(); WindowState = WindowState.Minimized; return;
            case "$browse":
            case "$all-apps": _vm.SetCategory("apps", true); FocusSelected(); return;
            case "$power": OpenPowerMenu(); return;
            case "$exit": Close(); return;
            case "$shutdown": ConfirmPower(false); return;
            case "$restart": ConfirmPower(true); return;
        }
        var item = tile.LaunchItem;
        if (item.Id.StartsWith('$')) { OpenSettings(); return; }
        _launching = true;
        try
        {
            SaveSelection();
            _vm.Status = $"正在打开 {tile.Name}…";
            var result = await _launch.LaunchAsync(item);
            _watch?.Cancel(); _watch?.Dispose(); _watch = null;
            _vm.Status = $"{tile.Name} 已打开 · Ctrl+Alt+H 返回首页";
            if (result.WaitForExit && result.Process != null)
            {
                _watch = new CancellationTokenSource();
                _ = WatchExitAsync(result.Process, tile.Name, _watch.Token);
            }
            else result.Process?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error($"启动失败：{tile.Id}", ex);
            _vm.Status = $"无法打开 {tile.Name}，请检查设置中的路径。";
            var dialog = new PowerDialog("无法打开 " + tile.Name, ex.Message, "打开设置");
            if (ShowModal(dialog) == true) OpenSettings(item.Id);
        }
        finally { _launching = false; }
    }
    private async Task WatchExitAsync(Process process, string name, CancellationToken token)
    {
        try
        {
            await process.WaitForExitAsync(token);
            if (token.IsCancellationRequested || _closed) return;
            _log.Info($"应用退出：{name}");
            _vm.Status = $"{name} 已退出，欢迎回来。";
            ReturnHome();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log.Error("等待应用退出失败", ex); }
        finally { process.Dispose(); }
    }
    private bool? ShowModal(Window dialog)
    {
        _dialog = dialog;
        dialog.Owner = this;
        try { return dialog.ShowDialog(); }
        finally { _dialog = null; FocusSelected(); }
    }
    private async void OpenSettings(string? selectId = null, bool addApp = false)
    {
        var settings = new SettingsWindow(_vm.Config, _config, _autoStart, _dataDirectory, selectId, addApp, _updates);
        if (ShowModal(settings) == true)
        {
            _vm.Refresh(settings.Result!, _dataDirectory);
            SetFullscreen(_vm.Config.StartFullscreen);
            _vm.Status = "设置已保存。"; FocusSelected();
            if (settings.InstallUpdateRequested)
            {
                string? id = null;
                try
                {
                    IsEnabled = false; _vm.Status = "正在准备安装，请稍候…";
                    _config.Save(_vm.Config);
                    var backupDirectory = Path.Combine(_dataDirectory, "Backups"); Directory.CreateDirectory(backupDirectory);
                    File.Copy(_config.ConfigPath, Path.Combine(backupDirectory, $"before-update-{UpdateProtocol.AppVersion}-{Guid.NewGuid():N}.json"));
                    id = await _updates.PrepareInstallAsync();
                    _config.Save(_vm.Config);
                    File.WriteAllText(Path.Combine(UpdateJobs.DirectoryFor(id), "go"), "go");
                    _updating = true; Close();
                }
                catch (Exception ex)
                {
                    if (id != null) File.WriteAllText(Path.Combine(UpdateJobs.DirectoryFor(id), "cancel"), "cancel");
                    IsEnabled = true; _log.Error("准备更新失败", ex); ShowModal(new PowerDialog("更新未开始", ex.Message, "知道了"));
                }
                finally { IsEnabled = true; }
            }
        }
    }
    private void ConfirmPower(bool restart)
    {
        var action = restart ? "重启" : "关机";
        if (ShowModal(new PowerDialog(restart ? "要重新启动电脑吗？" : "今天就到这里？", restart ? "Windows 将重新启动，请先保存其他应用中的工作。" : "确认后将正常关闭 Windows，请先保存其他应用中的工作。", action)) != true) return;
        try { SaveSelection(); new SystemPowerService(_log).Execute(restart); }
        catch (Exception ex) { _log.Error("电源操作失败", ex); ShowModal(new PowerDialog("操作未完成", ex.Message, "知道了")); }
    }
    private void OpenPowerMenu()
    {
        var menu = new PowerMenuWindow();
        if (ShowModal(menu) != true) return;
        switch (menu.Action)
        {
            case PowerMenuAction.Shutdown: ConfirmPower(false); break;
            case PowerMenuAction.Restart: ConfirmPower(true); break;
            case PowerMenuAction.Exit: Close(); break;
        }
    }
    private void SaveSelection()
    {
        try { _config.Save(_vm.Config); }
        catch (Exception ex) { _log.Error("保存配置失败", ex); _vm.Status = "配置未能保存，请检查目录写入权限。"; }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_updates.Busy && ! _updating) { e.Cancel = true; _updates.Cancel(); _vm.Status = "正在结束更新操作，请稍后再退出。"; return; }
        _closed = true; if (!_updating) SaveSelection(); _clock.Stop(); _updates.Dispose();
        _watch?.Cancel(); _watch?.Dispose();
        if (_source != null)
        {
            if (_hotkeyRegistered) WindowService.UnregisterHotKey(_source.Handle, 1);
            _source.RemoveHook(WindowMessage);
        }
    }
}
