using System.Collections.ObjectModel;
using System.IO;
using System.IO.Enumeration;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PTBox.Launcher.ViewModels;

public sealed record FilePickerEntry(string Name,string Path,bool IsDirectory)
{
    public string Glyph => IsDirectory ? "\uE8B7" : "\uE7C3";
    public string Kind => IsDirectory ? "文件夹" : System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
}

public sealed class FilePickerViewModel : ObservableObject
{
    private sealed class EntryCollection : ObservableCollection<FilePickerEntry>
    {
        public void Replace(IEnumerable<FilePickerEntry> entries)
        {
            Items.Clear(); foreach(var entry in entries) Items.Add(entry);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
    private readonly HashSet<string> _extensions;
    private readonly EntryCollection _entries = new();
    private CancellationTokenSource? _navigation;
    private FilePickerEntry[] _all=[];
    private int _request;
    private string _directory="", _status="", _search="";
    private bool _busy;
    public ObservableCollection<FilePickerEntry> Entries => _entries;
    public string DirectoryPath { get => _directory; private set => Set(ref _directory,value); }
    public string Status { get => _status; set => Set(ref _status,value); }
    public bool IsBusy { get => _busy; private set { if(Set(ref _busy,value)) Notify(nameof(CanBrowse)); } }
    public bool CanBrowse => !IsBusy;
    public string Search { get => _search; set { if(Set(ref _search,value)) Filter(); } }
    public FilePickerViewModel(IEnumerable<string> extensions) => _extensions=new(extensions,StringComparer.OrdinalIgnoreCase);
    public async Task NavigateAsync(string path)
    {
        _navigation?.Cancel();
        using var navigation = new CancellationTokenSource(); _navigation = navigation;
        var token = navigation.Token;
        var request=++_request;
        IsBusy=true; Status="正在读取文件夹…";
        try
        {
            path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')),DirectoryPath.Length>0 ? DirectoryPath : Environment.CurrentDirectory);
            var entries=await Task.Run(() =>
            {
                var files = new FileSystemEnumerable<FilePickerEntry>(path,
                    (ref FileSystemEntry entry) => new(entry.FileName.ToString(),entry.ToFullPath(),entry.IsDirectory),
                    new EnumerationOptions { AttributesToSkip=0, IgnoreInaccessible=false });
                files.ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                {
                    token.ThrowIfCancellationRequested();
                    return entry.IsDirectory || _extensions.Contains(Path.GetExtension(entry.FileName).ToString());
                };
                return files.OrderByDescending(x=>x.IsDirectory).ThenBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
            },token).WaitAsync(TimeSpan.FromSeconds(10),token);
            if(request!=_request) return;
            DirectoryPath=path; _all=entries;
            if(_search.Length>0) { _search=""; Notify(nameof(Search)); }
            Filter();
        }
        catch(OperationCanceledException) { }
        catch(TimeoutException)
        {
            navigation.Cancel();
            if(request==_request) Status="读取文件夹超时，请检查网络或同步状态，或切换到其他文件夹。";
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        { if(request==_request) Status="无法打开此文件夹，请检查路径或访问权限。"; }
        finally { if(ReferenceEquals(_navigation,navigation)) _navigation=null; if(request==_request) IsBusy=false; }
    }
    private void Filter()
    {
        _entries.Replace(_all.Where(x=>x.Name.Contains(Search,StringComparison.CurrentCultureIgnoreCase)));
        Status=Entries.Count==0 ? "没有找到符合类型的文件，可切换文件夹或修改筛选。" : $"{Entries.Count} 个项目 · 文件夹优先显示";
    }
    public string? SelectFile(string path)
    {
        try
        {
            var full=Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')),DirectoryPath.Length>0 ? DirectoryPath : Environment.CurrentDirectory);
            if(!_extensions.Contains(Path.GetExtension(full))) { Status="请选择允许类型的文件。"; return null; }
            if(!File.Exists(full)) { Status="文件不存在，请重新选择。"; return null; }
            return full;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { Status="文件路径无效。"; return null; }
    }
    public async Task<string?> SelectFileAsync(string path, CancellationToken token)
    {
        try
        {
            var full=Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')),DirectoryPath.Length>0 ? DirectoryPath : Environment.CurrentDirectory);
            if(!_extensions.Contains(Path.GetExtension(full))) { Status="请选择允许类型的文件。"; return null; }
            if(!await Task.Run(()=>File.Exists(full),token).WaitAsync(token)) { Status="文件不存在，请重新选择。"; return null; }
            return full;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { Status="文件路径无效。"; return null; }
    }
    public void Stop() { ++_request; _navigation?.Cancel(); IsBusy=false; }
}
