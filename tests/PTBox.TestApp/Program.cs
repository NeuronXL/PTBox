using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PTBox.TestApp;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var application = new Application();
        var contextIndex = Array.IndexOf(args, "--context-file");
        if (contextIndex >= 0 && contextIndex + 1 < args.Length)
            System.IO.File.WriteAllText(args[contextIndex + 1], System.Text.Json.JsonSerializer.Serialize(new { Directory = Environment.CurrentDirectory, Arguments = args }));
        var window = new Window { Title="PTBox 生命周期测试程序", Width=650, Height=340, WindowStartupLocation=WindowStartupLocation.CenterScreen };
        var close = new Button { Content="测试程序 · Enter 或点击关闭，返回首页", FontSize=24, Margin=new Thickness(24) };
        close.Click += (_, _) => window.Close();
        window.Content = close; window.Loaded += (_, _) => close.Focus();
        window.PreviewKeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Escape) window.Close(); };
        var readyIndex = Array.IndexOf(args, "--ready-file");
        if (readyIndex >= 0 && readyIndex + 1 < args.Length)
            window.ContentRendered += (_, _) => System.IO.File.WriteAllText(args[readyIndex + 1], "ready");
        var closeIndex = Array.IndexOf(args, "--close-file");
        if (closeIndex >= 0 && closeIndex + 1 < args.Length)
        {
            var closer = new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(50) };
            closer.Tick += (_, _) => { if (System.IO.File.Exists(args[closeIndex+1])) { closer.Stop(); window.Close(); } };
            closer.Start();
        }
        var lifetimeIndex = Array.IndexOf(args, "--lifetime");
        if (lifetimeIndex >= 0 && lifetimeIndex + 1 < args.Length && int.TryParse(args[lifetimeIndex + 1], out var lifetime))
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(lifetime) };
            timer.Tick += (_, _) => { timer.Stop(); window.Close(); }; timer.Start();
        }
        application.Run(window);
    }
}
