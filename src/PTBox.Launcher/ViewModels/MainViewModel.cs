using System.Collections.ObjectModel;
using System.Windows.Media;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;

namespace PTBox.Launcher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly NavigationService _navigation = new();
    private List<NavigationTarget> _targets = [];
    private List<TileViewModel> _all = [];
    private string _dataDirectory = "";
    private List<LauncherItem> _featured = [];
    public ObservableCollection<TileViewModel> Tabs { get; } = [];
    public ObservableCollection<TileViewModel> TopActions { get; } = [];
    public ObservableCollection<TileViewModel> HeroActions { get; } = [];
    public ObservableCollection<TileViewModel> HeroPages { get; } = [];
    public ObservableCollection<TileViewModel> SectionActions { get; } = [];
    public ObservableCollection<TileViewModel> Cards { get; } = [];
    public ObservableCollection<TileViewModel> AppTiles { get; } = [];
    public ObservableCollection<TileViewModel> QuickLinks { get; } = [];
    public ObservableCollection<TileViewModel> Footer { get; } = [];
    public IEnumerable<TileViewModel> All => _all;
    public LauncherConfig Config { get; private set; } = new();
    public int SelectedIndex { get; private set; }
    public TileViewModel Selected => _all[SelectedIndex];
    public string ActiveCategory { get; private set; } = "home";
    public int HeroIndex { get; private set; }
    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Clock => DateTime.Now.ToString("HH:mm");
    public string SectionHeading => ActiveCategory switch { "games" => "游戏时光", "streaming" => "大屏串流", "media" => "影视与视频", "apps" => "全部应用", _ => "常用应用" };
    public string CardCountLabel => Cards.Count > 6 ? $"{Cards.Count} 个应用 · 左右浏览" : "";
    public bool IsEmpty => Cards.Count == 0;
    private LauncherItem? FeaturedApp => _featured.ElementAtOrDefault(HeroIndex);
    public string HeroTitle => FeaturedApp == null ? "从第一个应用开始" : ArtworkService.Category(FeaturedApp) switch { "streaming" => "把主力性能，带到客厅", "media" => "把好时光，交给大屏", "games" => "精彩，从这里开始", _ => "你的应用，触手可及" };
    public string HeroSubtitle => FeaturedApp == null ? "添加 Steam、Moonlight，或你喜欢的应用与网页。" : "打开 " + AppName(FeaturedApp) + "，开启你的客厅时光。";
    private static string AppName(LauncherItem app) => TileViewModel.GetDisplayName(app);
    public Brush BackgroundBrush => new SolidColorBrush(Config.Theme == "Charcoal" ? Color.FromRgb(17,18,20) : Color.FromRgb(9,16,22));
    public Brush ThemeShade => Config.Theme == "Charcoal" ? new SolidColorBrush(Color.FromArgb(90,16,16,16)) : Brushes.Transparent;
    public ImageSource? BackgroundImage { get; private set; }
    public Brush HeroBackground { get; private set; } = Brushes.Transparent;

    public void Refresh(LauncherConfig config, string dataDirectory)
    {
        Config = config; _dataDirectory = dataDirectory;
        var previousHero = FeaturedApp?.Id;
        _featured = config.Apps.Take(3).ToList();
        HeroIndex = Math.Max(0, _featured.FindIndex(x => x.Id == previousHero));
        if (ActiveCategory is not ("home" or "apps") && !config.Apps.Any(x => ArtworkService.Category(x) == ActiveCategory)) ActiveCategory = "home";
        BackgroundImage = TileViewModel.LoadImage(config.Background, dataDirectory, 2560) ?? ArtworkService.Hero;
        BuildTabs(); BuildHero(); BuildCards(); BuildQuickLinks();
        Footer.Clear();
        Footer.Add(SystemTile("$settings", "设置")); Footer.Add(SystemTile("$desktop", "桌面")); Footer.Add(SystemTile("$power", "电源"));
        RebuildNavigation(config.LastSelectedTile);
        Notify(nameof(BackgroundImage)); Notify(nameof(BackgroundBrush)); Notify(nameof(ThemeShade));
    }
    private TileViewModel SystemTile(string id, string name, string subtitle = "", LauncherItem? target = null, string? artwork = null) =>
        new(new() { Id=id, Name=name, Subtitle=subtitle }, 0, _dataDirectory, target, artwork);
    private void BuildTabs()
    {
        Tabs.Clear(); TopActions.Clear();
        foreach (var (id, name) in new[] { ("home","首页"), ("games","游戏"), ("streaming","串流"), ("media","影视"), ("apps","应用") })
            if (id is "home" or "apps" || Config.Apps.Any(x => ArtworkService.Category(x) == id)) Tabs.Add(SystemTile("$tab-"+id,name));
        foreach (var tab in Tabs) tab.IsActive = tab.Id == "$tab-" + ActiveCategory;
        TopActions.Add(SystemTile("$top-settings", "设置"));
    }
    private void BuildHero()
    {
        HeroActions.Clear(); HeroPages.Clear();
        var app = FeaturedApp;
        HeroActions.Add(SystemTile("$hero-launch", app == null ? "添加应用" : "打开 " + AppName(app), target:app));
        HeroActions.Add(SystemTile("$browse", "浏览应用"));
        if (_featured.Count > 1)
        {
            for (var i=0;i<_featured.Count;i++) HeroPages.Add(SystemTile("$slide-"+i, AppName(_featured[i])));
            HeroPages[HeroIndex].IsActive = true;
        }
        HeroBackground = (HeroIndex == 0 || app == null) && BackgroundImage != null ? new ImageBrush(BackgroundImage) { Stretch=Stretch.UniformToFill, AlignmentY=AlignmentY.Top } : ArtworkService.Scene(app?.Id ?? "", app == null ? "apps" : ArtworkService.Category(app));
        Notify(nameof(HeroTitle)); Notify(nameof(HeroSubtitle)); Notify(nameof(HeroBackground));
    }
    private void BuildCards()
    {
        Cards.Clear(); AppTiles.Clear(); SectionActions.Clear();
        foreach (var item in Config.Apps.Where(x => ActiveCategory is "home" or "apps" || ArtworkService.Category(x) == ActiveCategory))
            Cards.Add(new(item,Cards.Count,_dataDirectory));
        foreach (var card in Cards) AppTiles.Add(card);
        AppTiles.Add(SystemTile("$add-app", "添加应用"));
        SectionActions.Add(SystemTile("$all-apps", "全部应用"));
        Notify(nameof(SectionHeading)); Notify(nameof(IsEmpty)); Notify(nameof(CardCountLabel));
    }
    private void BuildQuickLinks()
    {
        QuickLinks.Clear();
        foreach (var app in Config.Apps.GroupBy(ArtworkService.Category).Select(x => x.First()).Take(2))
            QuickLinks.Add(SystemTile("$quick-app-" + app.Id, "打开 " + AppName(app), app.Subtitle, app));
        if (Config.Apps.Count == 0) QuickLinks.Add(SystemTile("$quick-add", "添加应用", "从常用模板或本地程序开始"));
        QuickLinks.Add(SystemTile("$quick-desktop", "Windows 桌面", "返回熟悉的工作空间", artwork:"$desktop"));
    }
    public void SetCategory(string category, bool focusCards = false)
    {
        if (category is not ("home" or "games" or "streaming" or "media" or "apps")) return;
        if (!Tabs.Any(x => x.Id == "$tab-" + category)) return;
        ActiveCategory = category;
        foreach (var tab in Tabs) tab.IsActive = tab.Id == "$tab-" + category;
        BuildCards();
        RebuildNavigation(focusCards ? AppTiles[0].Id : "$tab-"+category);
        Notify(nameof(ActiveCategory));
    }
    public void SetHero(int index)
    {
        HeroIndex = Math.Clamp(index,0,Math.Max(0,_featured.Count-1)); BuildHero(); RebuildNavigation(HeroPages.Count > 0 ? "$slide-"+HeroIndex : "$hero-launch");
    }
    public void Back()
    {
        if (ActiveCategory != "home") SetCategory("home", true);
        Status = "你已在首页 · Ctrl+Alt+H 可从其他应用返回";
    }
    private void RebuildNavigation(string selectedId)
    {
        _all = []; _targets = [];
        void Add(IEnumerable<TileViewModel> tiles, int row, Func<int,int> x)
        { var position=0; foreach(var tile in tiles) { _targets.Add(new(_all.Count,row,x(position++))); _all.Add(tile); } }
        Add(Tabs,0,i=>220+i*100); Add(TopActions,0,_=>1300);
        Add(HeroActions,1,i=>140+i*250);
        Add(HeroPages,2,i=>20+i*26); Add(SectionActions,2,_=>1440);
        Add(AppTiles,3,i=>100+i*214);
        Add(QuickLinks,4,i=>(int)((i+.5)*1500/QuickLinks.Count)); Add(Footer,5,i=>1230+i*110);
        var selected = _all.FindIndex(x=>x.Id==selectedId);
        if(selected < 0) selected = _all.FindIndex(x=>x==AppTiles.FirstOrDefault());
        Select(selected < 0 ? _all.FindIndex(x=>x.Id=="$desktop") : selected);
    }
    public void Move(NavigationDirection direction) => Select(_navigation.Move(_targets, SelectedIndex, direction));
    public void Select(int index)
    {
        SelectedIndex = Math.Clamp(index,0,_all.Count-1);
        for(var i=0;i<_all.Count;i++) _all[i].IsSelected=i==SelectedIndex;
        Config.LastSelectedTile = Selected.Id;
        Notify(nameof(Selected));
    }
    public void Select(TileViewModel tile) { var index = _all.IndexOf(tile); if(index>=0) Select(index); }
    public void SelectById(string id) { var index = _all.FindIndex(x=>x.Id==id); if(index>=0) Select(index); }
    public void Tick() => Notify(nameof(Clock));
}
