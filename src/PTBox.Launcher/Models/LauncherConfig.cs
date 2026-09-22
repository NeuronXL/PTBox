namespace PTBox.Launcher.Models;

public sealed class LauncherConfig
{
    public int Version { get; set; } = 1;
    public List<LauncherItem> Apps { get; set; } = [];
    public string LastSelectedTile { get; set; } = "$add-app";
    public bool AutoStart { get; set; }
    public bool StartFullscreen { get; set; } = true;
    public string Theme { get; set; } = "Midnight";
    public string Background { get; set; } = "";
    public LauncherConfig Copy() => new()
    {
        Version = Version, Apps = Apps.Select(x => x.Copy()).ToList(),
        LastSelectedTile = LastSelectedTile, AutoStart = AutoStart,
        StartFullscreen = StartFullscreen, Theme = Theme, Background = Background
    };
}
