namespace PTBox.Launcher.Models;

public sealed class LauncherItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新应用";
    public string Subtitle { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Category { get; set; } = "auto";
    public string Type { get; set; } = "exe";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string LaunchBehavior { get; set; } = "waitForExit";
    public bool ReuseExisting { get; set; } = true;
    public string Accent { get; set; } = "#234B58";
    public LauncherItem Copy() => (LauncherItem)MemberwiseClone();
}
