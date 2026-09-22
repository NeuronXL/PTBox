using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PTBox.Launcher.Views;

public enum PowerMenuAction { None, Shutdown, Restart, Exit }
public partial class PowerMenuWindow : Window
{
    public PowerMenuAction Action { get; private set; }
    public PowerMenuWindow()
    {
        InitializeComponent(); Height = Math.Min(560, SystemParameters.WorkArea.Height * .95);
        Width = Height * 530 / 560;
        Loaded += (_, _) => CancelButton.Focus();
    }
    private void Cancel(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Choose(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string action } && Enum.TryParse<PowerMenuAction>(action, out var selected))
        { Action = selected; DialogResult = true; }
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Back or Key.BrowserBack) { e.Handled=true; DialogResult=false; }
        else if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            var buttons = new[] { CancelButton, ShutdownButton, RestartButton, ExitButton };
            var selected = Array.FindIndex(buttons,x=>x.IsKeyboardFocused);
            var delta = e.Key is Key.Up or Key.Left ? -1 : 1;
            buttons[Math.Clamp(selected+delta,0,buttons.Length-1)].Focus(); e.Handled=true;
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled=true;
            if(!e.IsRepeat && Keyboard.FocusedElement is Button button) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
}
