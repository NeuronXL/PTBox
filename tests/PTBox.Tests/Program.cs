using System.Diagnostics;
using System.IO;
using System.Text.Json;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;
using PTBox.Launcher.ViewModels;

namespace PTBox.Tests;

internal static class Program
{
    private static int _passed;
    [STAThread]
    private static int Main(string[] args)
    {
        var root = Path.Combine(Path.GetTempPath(), "PTBox.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Test("更新协议：版本、签名、平台、下载来源与安装参数", UpdateTests.Protocol);
            Test("GitHub 更新：缓存、过滤、下载、断网、限流、取消和坏包", () => UpdateTests.NetworkAsync(root).GetAwaiter().GetResult());
            Test("更新失败诊断：403 分类、限流冷却、无正式版本和网络故障", () => UpdateTests.FailureReportingAsync(root).GetAwaiter().GetResult());
            Test("安装版迁移：资源、冲突、幂等与未来格式保护", () => UpdateTests.Migration(root));
            Test("更新操作互斥、取消重试与偏好保存", () => UpdateTests.SingleFlightAsync(root).GetAwaiter().GetResult());
            Test("二维导航：边缘停止、同列上下、末行最近项", () =>
            {
                var nav = new NavigationService(); var targets = NavigationService.CreateTargets(8, 3);
                Equal(0, nav.Move(targets, 0, NavigationDirection.Left));
                Equal(0, nav.Move(targets, 0, NavigationDirection.Up));
                Equal(2, nav.Move(targets, 2, NavigationDirection.Right));
                Equal(5, nav.Move(targets, 2, NavigationDirection.Down));
                Equal(7, nav.Move(targets, 5, NavigationDirection.Down));
                Equal(9, nav.Move(targets, 7, NavigationDirection.Down));
                Equal(10, nav.Move(targets, 10, NavigationDirection.Down));
                Equal(7, nav.Move(targets, 10, NavigationDirection.Up));
            });
            Test("配置往返、选中项、顺序、中文和参数", () =>
            {
                var service = new ConfigService(root, new(root)); var config = LegacyConfig();
                config.LastSelectedTile = "moonlight"; config.Apps.Reverse();
                config.Apps[0].Arguments = "--app=\"https://example.com/?a=1&b=2\"";
                service.Save(config); var loaded = service.Load();
                Equal("moonlight", loaded.LastSelectedTile); Equal(config.Apps[0].Id, loaded.Apps[0].Id);
                Equal(config.Apps[0].Arguments, loaded.Apps[0].Arguments);
            });
            Test("损坏 JSON：备份并恢复默认", () =>
            {
                var service = new ConfigService(root, new(root)); File.WriteAllText(service.ConfigPath, "{broken");
                var config = service.Load(); Equal(0, config.Apps.Count);
                Check(service.LoadWarning != null, "缺少恢复提示");
                Check(Directory.GetFiles(Path.GetDirectoryName(service.ConfigPath)!, "*.broken").Length == 1, "缺少原文件备份");
                ConfigService.Parse(File.ReadAllText(service.ConfigPath));
            });
            Test("配置语义校验：重复 ID、保留 ID、空应用和 URI 生命周期", () =>
            {
                var config = LegacyConfig(); config.Apps[1].Id = config.Apps[0].Id;
                Throws<InvalidDataException>(() => ConfigService.Validate(config));
                config.Apps[1].Id = "$desktop"; Throws<InvalidDataException>(() => ConfigService.Validate(config));
                config.Apps = []; ConfigService.Validate(config);
                config.Apps = [new() { Type = "uri", Path = "steam://open/bigpicture", LaunchBehavior = "waitForExit" }];
                ConfigService.Validate(config); Equal("fireAndForget", config.Apps[0].LaunchBehavior);
                Throws<InvalidDataException>(() => ConfigService.Parse("null"));
            });
            Test("文件选择：目录导航、类型过滤、路径校验与关闭取消", () => FilePickerTests.Run(root).GetAwaiter().GetResult());
            Test("默认模板与内置配置保持一致", () =>
            {
                var bundled = ConfigService.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Config", "config.json")));
                Equal(JsonSerializer.Serialize(ConfigService.DefaultConfig(), ConfigService.JsonOptions), JsonSerializer.Serialize(bundled, ConfigService.JsonOptions));
            });
            Test("可选图标损坏不会影响首页", () =>
            {
                var vm = new MainViewModel(); var config = LegacyConfig();
                config.Apps[0].Icon = "missing.png"; config.Apps[0].Accent = "bad-color"; config.LastSelectedTile = "moonlight";
                vm.Refresh(config, root); Equal("moonlight", vm.Selected.Id); Equal(1, vm.All.Count(x => x.IsSelected));
                Check(vm.Cards[0].Icon == null, "损坏图标应回退");
                vm.Move(NavigationDirection.Down); Equal("$quick-app-playnite", vm.Selected.Id);
                var reordered = config.Copy(); reordered.Apps.Reverse(); vm.Refresh(reordered, root); Equal("files", vm.Selected.Id);
                vm.Refresh(new() { Apps = [], LastSelectedTile = "deleted" }, root); Equal("$add-app", vm.Selected.Id);
            });
            Test("电视首页分类、快捷入口、封面切换与全区导航", () =>
            {
                var vm = new MainViewModel(); vm.Refresh(LegacyConfig(), root);
                Check(vm.BackgroundImage != null, "内置背景必须成功加载");
                Check(vm.Cards.All(x => x.Artwork is System.Windows.Media.SolidColorBrush), "应用卡片应使用统一深色底");
                Equal(7,vm.Cards.Count); Equal("playnite",vm.Selected.Id);
                vm.SetCategory("games",true); Equal(2,vm.Cards.Count); Equal("playnite",vm.Selected.Id);
                vm.SetCategory("streaming",true); Equal("moonlight",vm.Selected.Id); Equal(1,vm.Cards.Count);
                vm.Back(); Equal("home",vm.ActiveCategory); Equal(7,vm.Cards.Count);
                Equal("playnite",vm.QuickLinks[0].LaunchItem.Id); Equal("moonlight",vm.QuickLinks[1].LaunchItem.Id);
                vm.SetHero(1); Equal("moonlight",vm.HeroActions[0].LaunchItem.Id);
                vm.SetHero(2); Equal("bilibili",vm.HeroActions[0].LaunchItem.Id);
                // Every actionable control must be reachable from the first app using D-pad only.
                var reached = new HashSet<string>(); var queue = new Queue<string>(); queue.Enqueue("playnite");
                while(queue.TryDequeue(out var id))
                {
                    if(!reached.Add(id)) continue;
                    foreach(var direction in Enum.GetValues<NavigationDirection>())
                    { vm.SelectById(id); vm.Move(direction); if(!reached.Contains(vm.Selected.Id)) queue.Enqueue(vm.Selected.Id); }
                }
                Equal(vm.All.Count(), reached.Count);
                var custom = LegacyConfig(); custom.Apps.Add(new() { Id="custom-stream", Category="streaming" });
                vm.Refresh(custom,root); vm.SetCategory("streaming",true); Equal(2,vm.Cards.Count);
            });
            Test("设置取消隔离与排序", () =>
            {
                var original = LegacyConfig(); var vm = new SettingsViewModel(original);
                vm.Selected!.Name = "已修改"; vm.Move(1);
                Equal("游戏", original.Apps[0].Name); Equal("playnite", original.Apps[0].Id);
                Equal("playnite", vm.Build().Apps[1].Id);
            });
            Test("应用删除、重排、空首页及配置持久化", () =>
            {
                var config = new LauncherConfig { Apps=[AppTemplateService.Create("steam"), AppTemplateService.Create("moonlight"), new() { Id="custom-movie", Name="我的影院", Category="media", Type="url", Path="https://example.com", LaunchBehavior="fireAndForget" }] };
                var vm = new MainViewModel(); vm.Refresh(config,root);
                Equal(3,vm.HeroPages.Count); vm.SetHero(2);
                Equal("custom-movie",vm.HeroActions[0].LaunchItem.Id); Check(vm.HeroSubtitle.Contains("我的影院"),"推荐文案必须使用实际应用名");
                vm.SetCategory("media"); config.Apps.RemoveAt(2); vm.Refresh(config,root);
                Equal("home",vm.ActiveCategory); Equal(2,vm.HeroPages.Count);
                Check(!vm.Tabs.Any(x=>x.Id=="$tab-media"),"删除最后一个影视应用后隐藏影视分类");
                config.Apps.Reverse(); config.Apps[0].Name="客厅串流"; vm.Refresh(config,root); vm.SetHero(0);
                Equal("moonlight",vm.Cards[0].Id); Equal("moonlight",vm.HeroActions[0].LaunchItem.Id); Check(vm.HeroSubtitle.Contains("客厅串流"),"改名应同步推荐区");
                config.Apps.RemoveAt(0); vm.Refresh(config,root);
                Equal(0,vm.HeroPages.Count); Check(!vm.HeroSubtitle.Contains("Moonlight"),"已移除应用不能残留推荐文案");
                Check(vm.QuickLinks.Where(x=>!x.LaunchItem.Id.StartsWith('$')).All(x=>config.Apps.Contains(x.LaunchItem)),"快捷入口只能指向已有应用");
                config.Apps.Clear(); vm.Refresh(config,root);
                Equal(0,vm.Cards.Count); Equal("$add-app",vm.Selected.Id); Equal("添加应用",vm.HeroActions[0].Name);
                Equal(0,vm.HeroPages.Count); Equal(2,vm.Tabs.Count);
                var service=new ConfigService(Path.Combine(root,"empty-reload"),new(root)); service.Save(config);
                Equal(0,service.Load().Apps.Count); Equal(0,ConfigService.DefaultConfig().Apps.Count);
                var legacy=LegacyConfig(); service.Save(legacy); Equal(7,service.Load().Apps.Count);
            });
            Test("不同应用数量下所有首页入口都能用方向键到达", () =>
            {
                foreach (var count in new[] { 0,1,2,3,7 })
                {
                    var config=LegacyConfig(); config.Apps=config.Apps.Take(count).ToList(); var vm=new MainViewModel(); vm.Refresh(config,root);
                    var reached=new HashSet<string>(); var pending=new Queue<string>(); pending.Enqueue(vm.Selected.Id);
                    while(pending.TryDequeue(out var id))
                    {
                        if(!reached.Add(id)) continue;
                        foreach(var direction in Enum.GetValues<NavigationDirection>()) { vm.SelectById(id); vm.Move(direction); if(!reached.Contains(vm.Selected.Id)) pending.Enqueue(vm.Selected.Id); }
                    }
                    Equal(vm.All.Count(),reached.Count); Equal(vm.All.Count(),vm.All.Select(x=>x.Id).Distinct().Count());
                }
            });
            Test("内置模板添加、重复防护与删除后重新添加", () =>
            {
                var original=ConfigService.DefaultConfig(); var settings=new SettingsViewModel(original);
                settings.Add(AppTemplateService.Create("steam")); settings.Add(AppTemplateService.Create("steam")); Equal(1,settings.Apps.Count);
                Equal("steam://open/bigpicture",settings.Selected!.Path); Equal("fireAndForget",settings.Selected.LaunchBehavior);
                settings.Add(AppTemplateService.Create("moonlight",@"D:\Apps\Moonlight.exe")); Equal("streaming",settings.Selected!.Category);
                Equal(@"D:\Apps\Moonlight.exe",settings.Selected.Path); Equal(0,original.Apps.Count);
                settings.Remove(); settings.Add(AppTemplateService.Create("moonlight")); Equal(2,settings.Apps.Count);
                var steam=AppTemplateService.Create("steam",@"D:\Steam\steam.exe"); Equal("exe",steam.Type); Equal("-bigpicture",steam.Arguments);
                ConfigService.Validate(settings.Build());
            });
            Test("程序原图标、自定义优先与缺失回退", () => IconTests.NativeAsync(root).GetAwaiter().GetResult());
            Test("网站图标、缓存、链接发现与失败回退", () => IconTests.WebsiteAsync(root).GetAwaiter().GetResult());
            Test("路径解析与缺失程序", () =>
            {
                Check(File.Exists(PathService.ResolveExecutable("explorer.exe", root)), "未找到 Explorer");
                Throws<FileNotFoundException>(() => PathService.ResolveExecutable("missing-PTBox-application.exe", root));
                Check(PathService.ResolveExecutable(@"%WINDIR%\explorer.exe", root).EndsWith("explorer.exe"), "环境变量路径解析错误");
            });
            Test("URL 与应用 URI 校验", () =>
            {
                AppLaunchService.ValidateUri(new() { Type="url", Path="https://www.bilibili.com" });
                AppLaunchService.ValidateUri(new() { Type="uri", Path="steam://open/bigpicture" });
                Throws<InvalidDataException>(() => AppLaunchService.ValidateUri(new() { Type="url", Path="steam://open/bigpicture" }));
                Throws<InvalidDataException>(() => AppLaunchService.ValidateUri(new() { Type="uri", Path="file:///C:/Windows/a.cmd" }));
            });
            Test("网页地址输入修正与具名错误", () =>
            {
                Equal("https://www.baidu.com", AppLaunchService.ValidateUri(new() { Name="百度", Type="url", Path="\uFEFF \u200Bhttps://www.baidu.com\r\n" }));
                Equal("https://www.baidu.com", AppLaunchService.ValidateUri(new() { Name="百度", Type="url", Path="https：／／www.baidu.com" }));
                const string content="https://example.com/中文：路径?q=全角／参数#片段";
                Equal(content, AppLaunchService.ValidateUri(new() { Type="url", Path=content }));
                Equal("steam://open/bigpicture", AppLaunchService.ValidateUri(new() { Type="uri", Path=" steam://open/bigpicture " }));
                foreach(var invalid in new[] { "", "www.baidu.com", "https://", "file:///C:/Windows/a.exe" })
                {
                    try { AppLaunchService.ValidateUri(new() { Name="测试网页", Type="url", Path=invalid }); throw new Exception("应拒绝无效地址"); }
                    catch(InvalidDataException ex) { Check(ex.Message.Contains("测试网页"),"错误需要标明具体应用"); }
                }
            });
            Test("单实例与再次运行唤回信号", () =>
            {
                using var first = new SingleInstanceService();
                Check(first.IsFirst, "测试时请先关闭正在运行的 PTBox");
                using var signaled = new ManualResetEventSlim(); first.Listen(() => signaled.Set());
                using var second = new SingleInstanceService();
                Check(!second.IsFirst, "第二实例不应成为主实例"); second.Signal();
                Check(signaled.Wait(TimeSpan.FromSeconds(3)), "唤回信号未到达");
            });
            if (args.Length > 0)
            {
                Test("桌面快捷方式：真实启动、参数、工作目录、图标与 URL 校验", () => ShortcutTests.Run(args[0], root).GetAwaiter().GetResult());
                Test("更新父进程身份：真实进程、路径、启动时间与版本", () => UpdateTests.ParentIdentityAsync(args[0], root).GetAwaiter().GetResult());
                Test("真实进程启动、复用、退出等待", () => TestLifecycle(args[0], root).GetAwaiter().GetResult());
            }
            if (args.Length > 1)
                Test("WPF 页面、键盘导航、对话框及 12 种分辨率/DPI 布局", () => UiSmoke.Run(args[1], root, args[0]));
            Console.WriteLine($"PASS: {_passed} test groups. Artifacts: {root}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    internal static LauncherConfig LegacyConfig() => ConfigService.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","legacy-config.json")));
    private static async Task TestLifecycle(string fixture, string root)
    {
        var launcher = new AppLaunchService(root, new(root));
        var readyFile = Path.Combine(root, "fixture-ready-" + Guid.NewGuid().ToString("N"));
        var closeFile = readyFile + ".close";
        var entry = new LauncherItem { Id="fixture", Name="生命周期测试", Path=Path.GetFullPath(fixture), Arguments=$"--lifetime 15000 --ready-file \"{readyFile}\" --close-file \"{closeFile}\"" };
        var launched = await launcher.LaunchAsync(entry);
        using var process = launched.Process!;
        try
        {
            Check(launched.WaitForExit && !launched.Reused, "首次启动应被跟踪");
            var timer = Stopwatch.StartNew();
            while ((!File.Exists(readyFile) || process.MainWindowHandle == IntPtr.Zero) && timer.Elapsed < TimeSpan.FromSeconds(8)) { await Task.Delay(100); process.Refresh(); }
            Check(File.Exists(readyFile), "测试应用应先完成内容渲染");
            Check(process.WaitForInputIdle(5000), "测试应用应先完成窗口初始化");
            var reused = await launcher.LaunchAsync(entry);
            using var existing = reused.Process!;
            Check(reused.Reused && existing.Id == process.Id, "复用应返回同一进程");
            // Ask the fixture to close its real WPF window on its dispatcher; avoid desktop-message timing races.
            File.WriteAllText(closeFile,"close");
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { if (!process.HasExited) process.Kill(); }
    }
    private static void Test(string name, Action action) { action(); _passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) => Check(Equals(expected, actual), $"Expected {expected}, got {actual}");
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
}
