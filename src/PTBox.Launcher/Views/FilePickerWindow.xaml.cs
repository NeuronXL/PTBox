using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PTBox.Launcher.ViewModels;

namespace PTBox.Launcher.Views;

public partial class FilePickerWindow : Window
{
    public string? SelectedPath { get; private set; }
    private readonly FilePickerViewModel _vm;
    private bool _updatingDrive;
    public FilePickerWindow(bool images,string? initialPath=null)
    {
        InitializeComponent();
        _vm=new(images ? [".png",".jpg",".jpeg",".bmp",".ico"] : [".exe"]); DataContext=_vm;
        Heading.Text=images ? "选择图片" : "选择程序";
        FilterHint.Text=images ? "图标或壁纸 · PNG / JPG / BMP / ICO" : "选择应用的 EXE 文件，也可以直接粘贴路径。";
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
        var start=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        try
        {
            var expanded=Environment.ExpandEnvironmentVariables(initialPath ?? "");
            if(Directory.Exists(expanded)) start=expanded;
            else if(File.Exists(expanded)) { start=Path.GetDirectoryName(Path.GetFullPath(expanded))!; FilePath.Text=Path.GetFullPath(expanded); }
        }
        catch(ArgumentException) { }
        var initialized=false;
        Loaded += async (_,_) => { if(initialized) return; initialized=true; await _vm.NavigateAsync(start); FileList.Focus(); };
        Closed += (_,_) => _vm.Stop();
    }
    private async void Go(object sender,RoutedEventArgs e) => await _vm.NavigateAsync(FolderPath.Text);
    private async void Up(object sender,RoutedEventArgs e)
    { if(_vm.DirectoryPath.Length>0 && Directory.GetParent(_vm.DirectoryPath) is DirectoryInfo parent) await _vm.NavigateAsync(parent.FullName); }
    private async void Shortcut(object sender,RoutedEventArgs e)
    {
        if(sender is Button { Tag:string folder } && Enum.TryParse<Environment.SpecialFolder>(folder,out var location))
        { var path=Environment.GetFolderPath(location); if(path.Length>0) await _vm.NavigateAsync(path); }
    }
    private async void DriveSelected(object sender,SelectionChangedEventArgs e)
    { if(!_updatingDrive && Drives.SelectedItem is string drive) await _vm.NavigateAsync(drive); }
    private void FileSelected(object sender,SelectionChangedEventArgs e)
    { FilePath.Text=FileList.SelectedItem is FilePickerEntry { IsDirectory:false } file ? file.Path : ""; }
    private void OpenSelected(object sender,MouseButtonEventArgs e) { if(FileList.SelectedItem!=null) Accept(sender,e); }
    private async void Accept(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(FilePath.Text) && FileList.SelectedItem is FilePickerEntry { IsDirectory:true } directory)
        { await _vm.NavigateAsync(directory.Path); return; }
        var file=_vm.SelectFile(FilePath.Text);
        if(file!=null) { SelectedPath=file; DialogResult=true; }
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
