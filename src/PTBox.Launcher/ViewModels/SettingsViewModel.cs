using System.Collections.ObjectModel;
using PTBox.Launcher.Models;

namespace PTBox.Launcher.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    public LauncherConfig Draft { get; }
    public ObservableCollection<LauncherItem> Apps { get; }
    private readonly string _dataDirectory;
    public string DataDirectory => _dataDirectory;
    public KeyValuePair<string,string>[] Types { get; } = [new("exe","Windows 程序"), new("url","网页链接"), new("uri","应用协议")];
    public KeyValuePair<string,string>[] Behaviors { get; } = [new("waitForExit","退出应用后返回首页"), new("fireAndForget","仅打开应用，手动返回")];
    public KeyValuePair<string,string>[] Themes { get; } = [new("Midnight","午夜蓝"), new("Charcoal","深炭灰")];
    public KeyValuePair<string,string>[] Categories { get; } =
    [new("auto","自动识别"), new("games","游戏"), new("streaming","串流"), new("media","影视"), new("apps","应用与工具")];
    private LauncherItem? _selected;
    public LauncherItem? Selected { get => _selected; set { if(Set(ref _selected, value)) { Notify(nameof(Preview)); Notify(nameof(HasSelection)); } } }
    public bool HasSelection => Selected != null;
    public TileViewModel? Preview => Selected == null ? null : new(Selected,0,_dataDirectory);
    private int _page;
    private void Page(int value) { if (Set(ref _page, value)) { Notify(nameof(IsAppsPage)); Notify(nameof(IsSystemPage)); Notify(nameof(IsUpdatePage)); } }
    public bool IsSystemPage { get => _page == 1; set => Page(value ? 1 : 0); }
    public bool IsUpdatePage { get => _page == 2; set => Page(value ? 2 : 0); }
    public bool IsAppsPage => _page == 0;
    public SettingsViewModel(LauncherConfig config, string dataDirectory = "")
    {
        _dataDirectory = dataDirectory;
        Draft = config.Copy(); Apps = new(Draft.Apps); Selected = Apps.FirstOrDefault();
    }
    public void Add(LauncherItem? item = null)
    {
        item ??= new();
        var existing = Apps.FirstOrDefault(x=>x.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
        if (existing != null) { Selected=existing; return; }
        if (Apps.Count >= 99) throw new InvalidOperationException("最多可添加 99 个应用。");
        Apps.Add(item); Selected = item;
    }
    public void Remove()
    {
        if (Selected == null) return;
        var index = Apps.IndexOf(Selected); Apps.Remove(Selected);
        Selected = Apps.Count == 0 ? null : Apps[Math.Min(index, Apps.Count - 1)];
    }
    public void Move(int delta)
    {
        if (Selected == null) return;
        var index = Apps.IndexOf(Selected); var target = index + delta;
        if (target >= 0 && target < Apps.Count) Apps.Move(index, target);
    }
    public LauncherConfig Build() { Draft.Apps = Apps.ToList(); return Draft; }
    public void RefreshSelected()
    {
        var item = Selected; Selected = null; Selected = item;
    }
    public void RefreshPreview() => Notify(nameof(Preview));
}
