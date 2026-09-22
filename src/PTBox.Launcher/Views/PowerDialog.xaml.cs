using System.Windows;
using System.Windows.Input;

namespace PTBox.Launcher.Views;

public partial class PowerDialog : Window
{
    public PowerDialog(string heading, string description, string confirm, string cancel = "取消 / 返回")
    {
        InitializeComponent(); Heading.Text = heading; Description.Text = description; ConfirmButton.Content = confirm;
        CancelButton.Content = cancel;
        var scale=Math.Min(1,Math.Min(SystemParameters.WorkArea.Width*.95/800,SystemParameters.WorkArea.Height*.95/410));
        Width=800*scale; Height=410*scale;
        Loaded += (_, _) => CancelButton.Focus();
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Back or Key.BrowserBack) { e.Handled = true; DialogResult = false; }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        { e.Handled = true; if (e.Key is Key.Left or Key.Up) CancelButton.Focus(); else ConfirmButton.Focus(); }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            if (!e.IsRepeat) DialogResult = ConfirmButton.IsKeyboardFocused;
        }
    }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void ConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
