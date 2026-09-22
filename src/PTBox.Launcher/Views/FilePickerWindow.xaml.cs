using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PTBox.Launcher.ViewModels;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;

namespace PTBox.Launcher.Views;

public partial class FilePickerWindow : Window
{
    public string? SelectedPath { get; private set; }
    public LauncherItem? SelectedApp { get; private set; }
    private readonly bool _images;
    private readonly FilePickerViewModel _vm;
    private bool _updatingDrive;
    private bool _selecting, _closed;
    private readonly CancellationTokenSource _lifetime = new();
    public FilePickerWindow(bool images,string? initialPath=null)
    {
        InitializeComponent();
        _images=images;
        _vm=new(images ? [".png",".jpg",".jpeg",".bmp",".ico"] : [".exe",".lnk",".url"]); DataContext=_vm;
        Heading.Text=images ? "选择图片" : "选择程序";
        FilterHint.Text=images ? "图标或壁纸 · PNG / JPG / BMP / ICO" : "支持 EXE、LNK、URL；桌面上找不到时，可查看公共桌面。";
        Drives.ItemsSource=Directory.GetLogicalDrives();
        _vm.PropertyChanged += (_,e) =>
        {
            if(e.PropertyName!=nameof(FilePickerViewModel.DirectoryPath)) return;
            _updatingDrive=true;
            try { Drives.SelectedItem=Path.GetPathRoot(_vm.DirectoryPath); }
            finally { _updatingDrive=false; }
        };
        var scale=Math.Min(1,Math.Min(SystemParameters.WorkArea.Width*.95/1140,SystemParameters.WorkArea.Height*.95/760));
        Width=1140*scale; Height=760*scale;
        var initialized=false;
        Loaded += async (_,_) =>
        {
            if(initialized) return; initialized=true;
            var start=Environment.GetFolderPath(images ? Environment.SpecialFolder.UserProfile : Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolderOption.DoNotVerify);
            if(start.Length==0) start=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile,Environment.SpecialFolderOption.DoNotVerify);
            // Initial-path probing may access disconnected drives, so keep it off the dispatcher too.
            if(!string.IsNullOrWhiteSpace(initialPath))
            {
                try
                {
                    var initial=await Task.Run(() =>
                    {
                        var expanded=Environment.ExpandEnvironmentVariables(initialPath);
                        return Directory.Exists(expanded) ? (Folder:expanded,File:"") : File.Exists(expanded) ? (Folder:Path.GetDirectoryName(Path.GetFullPath(expanded))!,File:Path.GetFullPath(expanded)) : (Folder:start,File:"");
                    }).WaitAsync(TimeSpan.FromSeconds(3),_lifetime.Token);
                    start=initial.Folder; FilePath.Text=initial.File;
                }
                catch(OperationCanceledException) { return; }
                catch(Exception ex) when(ex is ArgumentException or IOException or UnauthorizedAccessException or TimeoutException) { }
            }
            if(_closed || _vm.DirectoryPath.Length>0 || _vm.IsBusy) return;
            await _vm.NavigateAsync(start); if(!_closed) FileList.Focus();
        };
        Closed += (_,_) => { _closed=true; _lifetime.Cancel(); _lifetime.Dispose(); _vm.Stop(); };
    }
    private async void Go(object sender,RoutedEventArgs e) => await _vm.NavigateAsync(FolderPath.Text);
    private async void Up(object sender,RoutedEventArgs e)
    { if(_vm.DirectoryPath.Length>0 && Directory.GetParent(_vm.DirectoryPath) is DirectoryInfo parent) await _vm.NavigateAsync(parent.FullName); }
    private async void Shortcut(object sender,RoutedEventArgs e)
    {
        if(sender is Button { Tag:string folder } && Enum.TryParse<Environment.SpecialFolder>(folder,out var location))
        { var path=Environment.GetFolderPath(location,Environment.SpecialFolderOption.DoNotVerify); if(path.Length>0) await _vm.NavigateAsync(path); }
    }
    private async void DriveSelected(object sender,SelectionChangedEventArgs e)
    { if(!_updatingDrive && Drives.SelectedItem is string drive) await _vm.NavigateAsync(drive); }
    private void FileSelected(object sender,SelectionChangedEventArgs e)
    { FilePath.Text=FileList.SelectedItem is FilePickerEntry { IsDirectory:false } file ? file.Path : ""; }
    private void OpenSelected(object sender,MouseButtonEventArgs e) { if(FileList.SelectedItem!=null) Accept(sender,e); }
    private async void Accept(object sender,RoutedEventArgs e)
    {
        if(_selecting || _vm.IsBusy || _closed) return;
        if(string.IsNullOrWhiteSpace(FilePath.Text) && FileList.SelectedItem is FilePickerEntry { IsDirectory:true } directory)
        { await _vm.NavigateAsync(directory.Path); return; }
        _selecting=true; SelectButton.IsEnabled=false; FolderNavigation.IsEnabled=false; BrowserArea.IsEnabled=false; FilePath.IsEnabled=false;
        using var selection=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        selection.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var file=await _vm.SelectFileAsync(FilePath.Text,selection.Token);
            if(file==null || _closed) return;
            _vm.Status=_images ? "正在读取文件…" : "正在读取程序或快捷方式…";
            var app = _images ? null : await Task.Run(()=>AppLaunchService.FromLocalFile(file),selection.Token).WaitAsync(selection.Token);
            if(_closed) return;
            SelectedApp=app;
            SelectedPath=file; DialogResult=true;
        }
        catch(OperationCanceledException) { if(!_closed) _vm.Status="读取超时，请检查文件是否在可访问的本地目录，或重新选择。"; }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { if(!_closed) _vm.Status=ex.Message; }
        finally
        {
            _selecting=false;
            if(!_closed) { SelectButton.IsEnabled=true; FolderNavigation.IsEnabled=true; BrowserArea.IsEnabled=true; FilePath.IsEnabled=true; }
        }
    }
    private void Cancel(object sender,RoutedEventArgs e) => DialogResult=false;
    private void OnKeyDown(object sender,KeyEventArgs e)
    {
        if(Drives.IsDropDownOpen) { if(e.Key is Key.Escape or Key.BrowserBack or Key.Back) { Drives.IsDropDownOpen=false; e.Handled=true; } return; }
        if(e.Key is Key.Escape or Key.BrowserBack || e.Key==Key.Back && Keyboard.FocusedElement is not TextBox) { e.Handled=true; DialogResult=false; return; }
        if(Keyboard.FocusedElement is TextBox text)
        {
            if(e.Key==Key.Enter) { if(text==FolderPath) Go(sender,e); else if(text==FilePath) Accept(sender,e); else FileList.Focus(); e.Handled=true; }
            else if(e.Key is Key.Up or Key.Down) { text.MoveFocus(new TraversalRequest(e.Key==Key.Down ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous)); e.Handled=true; }
            return;
        }
        if(e.Key==Key.Enter)
        {
            e.Handled=true; if(e.IsRepeat) return;
            if(Keyboard.FocusedElement is ComboBox combo) combo.IsDropDownOpen=true;
            else if(Keyboard.FocusedElement is Button button) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else Accept(sender,e);
        }
        else if(e.Key is Key.Left or Key.Right || e.Key is Key.Up or Key.Down && Keyboard.FocusedElement is not (ListBoxItem or ListBox))
        {
            var direction=e.Key switch { Key.Left=>FocusNavigationDirection.Left,Key.Right=>FocusNavigationDirection.Right,Key.Up=>FocusNavigationDirection.Up,_=>FocusNavigationDirection.Down };
            if(Keyboard.FocusedElement is UIElement element) { element.MoveFocus(new TraversalRequest(direction)); e.Handled=true; }
        }
    }
}
