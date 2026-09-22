using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;
using PTBox.Launcher.ViewModels;

namespace PTBox.Launcher.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly ConfigService _config;
    private readonly AutoStartService _autoStart;
    private readonly string _dataDirectory;
    public LauncherConfig? Result { get; private set; }
    public bool InstallUpdateRequested { get; private set; }
    private readonly UpdateService _updates;
    private readonly bool _ownsUpdates;
    public SettingsWindow(LauncherConfig config, ConfigService service, AutoStartService autoStart, string dataDirectory, string? selectId, bool addApp = false, UpdateService? updates = null)
    {
        _dataDirectory = dataDirectory;
        _vm = new(config, dataDirectory); _config = service; _autoStart = autoStart;
        InitializeComponent(); DataContext = _vm;
        _ownsUpdates = updates == null; _updates = updates ?? new UpdateService(dataDirectory); UpdatePanel.DataContext = _updates;
        Closed += (_, _) => { if (_ownsUpdates) _updates.Dispose(); };
        DataPath.Text = "配置：" + service.ConfigPath;
        if (selectId != null) _vm.Selected = _vm.Apps.FirstOrDefault(x => x.Id == selectId);
        var scale = Math.Min(.9, Math.Min(SystemParameters.WorkArea.Width * .95 / 1600, SystemParameters.WorkArea.Height * .95 / 900));
        Width = 1600 * scale; Height = 900 * scale;
        UpdateBackground();
        Loaded += (_, _) =>
        {
            if(_vm.Selected != null && AppList.ItemContainerGenerator.ContainerFromItem(_vm.Selected) is ListBoxItem item) item.Focus();
            else AppList.Focus();
            if (addApp) Dispatcher.BeginInvoke(new Action(() => AddItem(this, new RoutedEventArgs())));
        };
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Let a dropdown consume Back/Escape before treating it as leaving settings.
        var openCombo = new[] { TypePicker, CategoryPicker, BehaviorPicker, ThemePicker }.FirstOrDefault(x => x.IsDropDownOpen);
        if (openCombo != null)
        {
            if (e.Key is Key.Escape or Key.Back or Key.BrowserBack) { openCombo.IsDropDownOpen=false; e.Handled=true; }
            return;
        }
        if (e.Key is Key.Escape or Key.BrowserBack || e.Key == Key.Back && Keyboard.FocusedElement is not TextBox)
        { e.Handled = true; DialogResult = false; return; }
        if (Keyboard.FocusedElement is TextBox text)
        {
            if (e.Key is Key.Up or Key.Down && !text.AcceptsReturn)
            { e.Handled = true; text.MoveFocus(new TraversalRequest(e.Key == Key.Down ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous)); }
            return;
        }
        if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true }) return;
        if (e.Key == Key.Enter && Keyboard.FocusedElement is ComboBox combo) { combo.IsDropDownOpen = true; e.Handled = true; return; }
        if (e.Key == Key.Enter && Keyboard.FocusedElement is CheckBox check) { check.IsChecked = !check.IsChecked; e.Handled = true; return; }
        if (Keyboard.FocusedElement is ListBoxItem && e.Key is Key.Up or Key.Down) return;
        if (ReleaseNotesScroll.IsKeyboardFocused && (e.Key == Key.Up && ReleaseNotesScroll.VerticalOffset > 0 || e.Key == Key.Down && ReleaseNotesScroll.VerticalOffset < ReleaseNotesScroll.ScrollableHeight))
        { ReleaseNotesScroll.ScrollToVerticalOffset(ReleaseNotesScroll.VerticalOffset + (e.Key == Key.Down ? 80 : -80)); e.Handled = true; return; }
        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            var direction = e.Key switch { Key.Up => FocusNavigationDirection.Up, Key.Down => FocusNavigationDirection.Down, Key.Left => FocusNavigationDirection.Left, _ => FocusNavigationDirection.Right };
            if (Keyboard.FocusedElement is UIElement element && element.MoveFocus(new TraversalRequest(direction))) e.Handled = true;
        }
    }
    private void ShowApps(object sender, RoutedEventArgs e) => _vm.IsSystemPage = false;
    private void ShowSystem(object sender, RoutedEventArgs e) => _vm.IsSystemPage = true;
    private void ShowUpdates(object sender, RoutedEventArgs e) => _vm.IsUpdatePage = true;
    private async void CheckUpdate(object sender, RoutedEventArgs e) => await _updates.CheckAsync();
    private async void DownloadUpdate(object sender, RoutedEventArgs e) => await _updates.DownloadAsync();
    private void CancelUpdate(object sender, RoutedEventArgs e) => _updates.Cancel();
    private void OpenReleases(object sender, RoutedEventArgs e) { try { _updates.OpenReleases(); } catch (Exception ex) { Status.Text = ex.Message; } }
    private void OpenDownloads(object sender, RoutedEventArgs e) { try { _updates.OpenDownloads(); } catch (Exception ex) { Status.Text = ex.Message; } }
    private void InstallUpdate(object sender, RoutedEventArgs e)
    {
        var confirm = new PowerDialog("保存设置并更新 PTBox？", "将保存当前设置，退出 PTBox 后安装新版。游戏和 Moonlight 将继续运行，Windows 不会重新启动。", "保存并更新") { Owner = this };
        if (confirm.ShowDialog() != true) return;
        InstallUpdateRequested = true; Save(sender, e);
        if (Result == null) InstallUpdateRequested = false;
    }
    private void OnSelectedAppChanged(object sender, SelectionChangedEventArgs e) => EditorScroll?.ScrollToTop();
    private void AddItem(object sender, RoutedEventArgs e)
    {
        if (_vm.Apps.Count >= 99) { Status.Text="最多可添加 99 个应用。"; return; }
        var picker = new AddAppWindow(_vm.Apps) { Owner=this };
        if (picker.ShowDialog() != true || picker.Result == null) return;
        _vm.IsSystemPage=false; _vm.Add(picker.Result);
        AppList.ScrollIntoView(_vm.Selected); EditorScroll.ScrollToTop();
        Status.Text="已加入草稿，可调整名称和路径；点击保存设置后出现在首页。";
        AppName.Focus();
    }
    private void RemoveItem(object sender, RoutedEventArgs e) => _vm.Remove();
    private void MoveUp(object sender, RoutedEventArgs e) => _vm.Move(-1);
    private void MoveDown(object sender, RoutedEventArgs e) => _vm.Move(1);
    private void RefreshList(object sender, KeyboardFocusChangedEventArgs e) { AppList.Items.Refresh(); _vm.RefreshPreview(); }
    private void OnAddressChanged(object sender, TextChangedEventArgs e)
    {
        if (Status?.Text.StartsWith("保存失败：", StringComparison.Ordinal) == true) Status.Text = "";
    }
    private void BrowseExe(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        var dialog = Picker(false, _vm.Selected.Path);
        if (dialog.ShowDialog() == true && dialog.SelectedApp is { } selected)
        {
            _vm.Selected.Path = selected.Path; _vm.Selected.Type = selected.Type;
            if (!string.Equals(Path.GetExtension(dialog.SelectedPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                _vm.Selected.Arguments = selected.Arguments;
                _vm.Selected.ReuseExisting = selected.ReuseExisting;
                _vm.Selected.LaunchBehavior = selected.LaunchBehavior;
            }
            _vm.RefreshSelected();
        }
    }
    private FilePickerWindow Picker(bool images, string? initial)
    {
        try { if (!string.IsNullOrWhiteSpace(initial)) initial = images ? PathService.ResolveAsset(initial, _dataDirectory) : PathService.ResolveExecutable(initial, _dataDirectory); }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException) { initial = null; }
        return new FilePickerWindow(images, initial) { Owner = this };
    }
    private void BrowseIcon(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        var dialog = Picker(true, _vm.Selected.Icon);
        if (dialog.ShowDialog() == true) { _vm.Selected.Icon = dialog.SelectedPath!; _vm.RefreshSelected(); }
    }
    private void BrowseBackground(object sender, RoutedEventArgs e)
    {
        var dialog = Picker(true, _vm.Draft.Background);
        if (dialog.ShowDialog() == true) { BackgroundPath.Text = dialog.SelectedPath!; UpdateBackground(); }
    }
    private void ResetBackground(object sender, RoutedEventArgs e) { BackgroundPath.Text=""; UpdateBackground(); }
    private void RefreshBackground(object sender, KeyboardFocusChangedEventArgs e) => UpdateBackground();
    private void UpdateBackground() => BackgroundPreview.ImageSource = TileViewModel.LoadImage(_vm.Draft.Background,_dataDirectory,2560) ?? ArtworkService.Hero;
    private void Save(object sender, RoutedEventArgs e)
    {
        var previousAutoStart = _autoStart.IsEnabled();
        var changedAutoStart = false;
        try
        {
            var result = _vm.Build(); ConfigService.Validate(result);
            foreach (var item in result.Apps.Where(x => x.Type != "exe"))
            {
                try { item.Path = AppLaunchService.ValidateUri(item); }
                catch (InvalidDataException)
                {
                    _vm.IsSystemPage = false; _vm.Selected = item;
                    AppList.ScrollIntoView(item); AppPath.Focus(); AppPath.SelectAll();
                    throw;
                }
            }
            if (result.AutoStart != previousAutoStart) { _autoStart.SetEnabled(result.AutoStart); changedAutoStart = true; }
            _config.Save(result);
            Result = result; DialogResult = true;
        }
        catch (Exception ex)
        {
            Status.Text = "保存失败：" + ex.Message;
            if (changedAutoStart)
            {
                try { _autoStart.SetEnabled(previousAutoStart); }
                catch (Exception rollback) { Status.Text += "；请检查自动启动状态：" + rollback.Message; }
            }
        }
    }
    private void Cancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
