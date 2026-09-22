using System.Windows.Media;
using System.Windows.Media.Imaging;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;

namespace PTBox.Launcher.ViewModels;

public sealed class TileViewModel : ObservableObject
{
    public LauncherItem Item { get; }
    public string Id => Item.Id;
    public string Name => Item.Name;
    public string DisplayName => GetDisplayName(Item);
    public static string GetDisplayName(LauncherItem item) => item.Id switch { "playnite" when item.Name == "游戏" => "Playnite", "moonlight" when item.Name == "串流" => "Moonlight", "edge" when item.Name == "浏览器" => "Edge", _ => item.Name };
    public string Subtitle => Item.Subtitle;
    public string Number { get; }
    public string Glyph { get; }
    public Brush Accent { get; }
    public ImageSource? Icon { get; }
    private ImageSource? _displayIcon;
    public ImageSource? DisplayIcon { get => _displayIcon; private set => Set(ref _displayIcon,value); }
    public Task IconReady { get; }
    public Brush Artwork { get; }
    public LauncherItem LaunchItem { get; }
    private bool _active;
    public bool IsActive { get => _active; set => Set(ref _active, value); }
    private bool _selected;
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
    public TileViewModel(LauncherItem item, int index, string dataDirectory, LauncherItem? launchItem = null, string? artworkId = null)
    {
        Item = item;
        LaunchItem = launchItem ?? item;
        Number = (index + 1).ToString("00");
        Glyph = (launchItem?.Id ?? item.Id) switch
        {
            "$desktop" or "$quick-desktop" => "\uE7F4", "$settings" or "$top-settings" => "\uE713", "$exit" => "\uE8BB", "$restart" => "\uE777", "$shutdown" or "$power" => "\uE7E8",
            "$add-app" or "$quick-add" => "\uE710", "$hero-launch" => "\uE710", "$browse" or "$all-apps" => "\uE80A", _ => LaunchItem.Type == "url" ? "\uE774" : "\uE80A"
        };
        if (item.Id == "$hero-launch" && launchItem != null) Glyph = "\uE768";
        try { Accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.Accent)); }
        catch { Accent = new SolidColorBrush(Color.FromRgb(35, 75, 88)); }
        Accent.Freeze();
        Icon = LoadImage((launchItem ?? item).Icon, dataDirectory);
        DisplayIcon = Icon;
        Artwork = AppIconService.CardBackground;
        IconReady = Icon == null && !LaunchItem.Id.StartsWith('$') ? LoadAppIconAsync(LaunchItem.Copy(),dataDirectory) : Task.CompletedTask;
    }
    private async Task LoadAppIconAsync(LauncherItem app,string dataDirectory)
    {
        try { DisplayIcon=await AppIconService.LoadAsync(app,dataDirectory); }
        catch { } // Keep the generic glyph if any optional icon provider fails.
    }
    public static ImageSource? LoadImage(string path, string dataDirectory, int decodePixelWidth = 512)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodePixelWidth; image.UriSource = new Uri(PathService.ResolveAsset(path, dataDirectory));
            image.EndInit(); image.Freeze(); return image;
        }
        catch { return null; } // User-supplied images are optional.
    }
}
