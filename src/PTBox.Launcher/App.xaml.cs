using System.Windows;
using PTBox.Launcher.Services;
using PTBox.Launcher.Views;
using PTBox.UpdateCore;

namespace PTBox.Launcher;

public partial class App : Application
{
    private SingleInstanceService? _instance;
    public LoggingService Log { get; private set; } = null!;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _instance = new SingleInstanceService();
        if (!_instance.IsFirst) { _instance.Signal(); Shutdown(); return; }
        var dataDirectory = ConfigService.ChooseDataDirectory();
        string? migrationWarning = null;
        if (DataMigrationService.IsInstalled(AppContext.BaseDirectory))
        {
            try
            {
                dataDirectory = DataMigrationService.Migrate(AppContext.BaseDirectory, dataDirectory, (source, target) =>
                    new PowerDialog("发现两份 PTBox 配置", "旧安装目录和个人数据目录都已有配置。请选择继续使用哪一份，另一份会保留。\n旧安装：" + source + "\n个人数据：" + target, "使用旧安装配置", "使用已有配置").ShowDialog() == true);
            }
            catch (Exception ex)
            {
                // A failed migration must not silently seed or overwrite the destination.
                dataDirectory = AppContext.BaseDirectory;
                migrationWarning = "配置迁移未完成，继续使用原配置：" + ex.Message;
            }
        }
        Log = new LoggingService(dataDirectory);
        Log.Info("PTBox 启动");
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("UI 未处理异常", args.Exception);
            MessageBox.Show("发生错误，详细信息已写入 Logs/launcher.log。\n" + args.Exception.Message, "PTBox", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("进程未处理异常", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Error("后台任务异常", args.Exception); args.SetObserved(); };
        var configService = new ConfigService(dataDirectory, Log);
        var window = new MainWindow(configService, Log, dataDirectory);
        if (migrationWarning != null) Log.Info(migrationWarning);
        MainWindow = window;
        _instance.Listen(() => Dispatcher.BeginInvoke(() => window.ReturnHome()));
        window.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        if (migrationWarning != null) new PowerDialog("配置迁移未完成", migrationWarning, "知道了") { Owner = window }.ShowDialog();
        if (e.Args.Length == 2 && e.Args[0] == "--update-complete")
            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try { if (!configService.IsReadOnly && configService.LoadWarning == null && migrationWarning == null) UpdateJobs.AcknowledgeStartup(e.Args[1], AppContext.BaseDirectory); }
                catch (Exception ex) { Log.Error("更新启动确认失败", ex); }
            }));
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        Log?.Info("PTBox 退出");
        base.OnExit(e);
    }
}
