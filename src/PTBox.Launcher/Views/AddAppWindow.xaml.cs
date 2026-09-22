using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;

namespace PTBox.Launcher.Views;

public partial class AddAppWindow : Window
{
    private readonly IReadOnlyList<AppTemplate> _templates;
    public LauncherItem? Result { get; private set; }
    public AddAppWindow(IEnumerable<LauncherItem> existing)
    {
        InitializeComponent();
        _templates = AppTemplateService.Discover();
        SteamStatus.Text = _templates[0].Status; MoonlightStatus.Text = _templates[1].Status;
        foreach (var button in new[] { SteamButton, MoonlightButton })
            if (existing.Any(x => x.Id.Equals((string)button.Tag, StringComparison.OrdinalIgnoreCase)))
            {
                button.IsEnabled = false;
                if (button == SteamButton) SteamStatus.Text = "已添加，可在应用管理中调整";
                else MoonlightStatus.Text = "已添加，可在应用管理中调整";
            }
        var scale = Math.Min(1, Math.Min(SystemParameters.WorkArea.Height * .95 / 700, SystemParameters.WorkArea.Width * .95 / 660));
        Width = 660 * scale; Height = 700 * scale;
        Loaded += (_, _) => Buttons().First().Focus();
    }
    private Button[] Buttons() => new[] { SteamButton, MoonlightButton, LocalButton, WebsiteButton, CancelButton }.Where(x=>x.IsEnabled).ToArray();
    private void ChooseTemplate(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        var template = _templates.Single(x=>x.Id==id);
        Result = AppTemplateService.Create(id, template.DetectedPath); DialogResult=true;
    }
    private void ChooseLocal(object sender, RoutedEventArgs e)
    {
        var picker = new FilePickerWindow(false) { Owner=this };
        if (picker.ShowDialog() != true) return;
        Result = new() { Name=Path.GetFileNameWithoutExtension(picker.SelectedPath!), Path=picker.SelectedPath!, Category="apps" }; DialogResult=true;
    }
    private void ChooseWebsite(object sender, RoutedEventArgs e)
    { Result = new() { Name="新网页", Type="url", Category="apps", LaunchBehavior="fireAndForget", ReuseExisting=false }; DialogResult=true; }
    private void Cancel(object sender, RoutedEventArgs e) => DialogResult=false;
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Back or Key.BrowserBack) { DialogResult=false; e.Handled=true; }
        else if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            var buttons=Buttons(); var index=Array.FindIndex(buttons,x=>x.IsKeyboardFocused);
            buttons[Math.Clamp(index+(e.Key is Key.Up or Key.Left ? -1 : 1),0,buttons.Length-1)].Focus(); e.Handled=true;
        }
        else if (e.Key is Key.Enter or Key.Space)
        { e.Handled=true; if (!e.IsRepeat && Keyboard.FocusedElement is Button button) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    }
}
