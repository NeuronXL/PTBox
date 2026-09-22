using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PTBox.Updater;

public sealed class UpdaterWindow : Window
{
    private readonly TextBlock _status;
    private readonly Button _close;
    public bool IsUpdating { get; private set; } = true;
    public UpdaterWindow()
    {
        Title = "PTBox · 更新"; Width = 740; Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent; Foreground = Brushes.White; ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PTBox.Updater;component/SettingsStyles.xaml", UriKind.Relative) });
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "PTBox 更新", FontSize = 30, FontWeight = FontWeights.SemiBold });
        _status = new TextBlock { Text = "正在验证安装包…", FontSize = 19, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 24, 0, 24) };
        panel.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 230, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _close = new Button { Content = "关闭", Visibility = Visibility.Collapsed }; panel.Children.Add(_close);
        _close.Click += (_, _) => Close();
        Content = new Border { Padding = new Thickness(36), CornerRadius = new CornerRadius(26), BorderBrush = new SolidColorBrush(Color.FromRgb(72, 97, 122)), BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.FromRgb(14, 26, 40)), Child = panel };
        Closing += (_, e) => e.Cancel = IsUpdating;
        PreviewKeyDown += (_, e) => { if (!IsUpdating && e.Key is Key.Enter or Key.Escape or Key.BrowserBack) { e.Handled = true; Close(); } };
    }
    public void Report(string message) => _status.Text = message;
    public void Complete() { IsUpdating = false; Close(); }
    public void ShowFailure(string message)
    {
        Report(message); IsUpdating = false; _close.Visibility = Visibility.Visible; _close.Focus(); Activate();
    }
}
