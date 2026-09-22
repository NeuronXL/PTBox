using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;
using PTBox.UpdateCore;

namespace PTBox.Tests;

internal static class UpdateTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception ex) when (ex is InvalidDataException or CryptographicException or UnsupportedConfigVersionException) { return; }
        throw new Exception("Expected rejection");
    }
    private static async Task RejectAsync(Func<Task> action)
    {
        try { await action(); } catch (Exception ex) when (ex is InvalidDataException or HttpRequestException or OperationCanceledException) { return; }
        throw new Exception("Expected async rejection");
    }
    private static ReleaseCandidate Candidate(RSA rsa, byte[] package, UpdateManifest? custom = null)
    {
        var m = custom ?? new UpdateManifest(1, "1.2.0", "stable", "win-x64", 22000, "1.0.0", "PTBox-Setup-1.2.0-win-x64.exe",
            "https://github.com/NeuronXL/PTBox/releases/download/v1.2.0/PTBox-Setup-1.2.0-win-x64.exe", package.Length,
            Convert.ToHexString(SHA256.HashData(package)), DateTimeOffset.UtcNow, "test");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(m, UpdateProtocol.Json);
        return new(m, bytes, rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), "更新说明");
    }
    public static void Protocol()
    {
        Check(UpdateProtocol.ParseVersion("1.0.10") > UpdateProtocol.ParseVersion("1.0.9"), "数字版本比较");
        foreach (var value in new[] { "v1.0.0", "1.0", "1.0.0-beta", "01.0.0", "1.0.0.1", "../1.0.0" }) Reject(() => UpdateProtocol.ParseVersion(value));
        using var key = RSA.Create(2048); var trust = new ReleaseTrust("NeuronXL/PTBox", new() { ["test"] = key.ExportSubjectPublicKeyInfoPem() });
        var release = Candidate(key, [1, 2, 3]);
        Check(UpdateProtocol.VerifyManifest(release.ManifestBytes, release.Signature, trust).Version == "1.2.0", "签名清单往返");
        var corrupt = (byte[])release.ManifestBytes.Clone(); corrupt[^2] ^= 1;
        Reject(() => UpdateProtocol.VerifyManifest(release.ManifestBytes, new byte[256], trust));
        Reject(() => UpdateProtocol.VerifyManifest(release.ManifestBytes, release.Signature, new("NeuronXL/PTBox", new())));
        foreach (var m in new[]
        {
            release.Manifest with { Rid = "win-arm64" }, release.Manifest with { SchemaVersion = 2 },
            release.Manifest with { Channel = "beta" }, release.Manifest with { Size = UpdateProtocol.MaxPackageSize + 1 },
            release.Manifest with { DownloadUrl = "https://evil.example/package.exe" }, release.Manifest with { AssetName = "../bad.exe" },
            release.Manifest with { Sha256 = "bad" }
        }) { var candidate = Candidate(key, [], m); Reject(() => UpdateProtocol.VerifyManifest(candidate.ManifestBytes, candidate.Signature, trust)); }
        Reject(() => UpdateProtocol.CheckCompatibility(release.Manifest, "1.2.0", 99999));
        Reject(() => UpdateProtocol.CheckCompatibility(release.Manifest with { MinUpdaterVersion = "2.0.0" }, "1.0.0", 99999));
        Reject(() => UpdateProtocol.CheckCompatibility(release.Manifest, "1.0.0", 19045));
        foreach (var url in new[] { "http://github.com/a", "https://github.com.evil.example/a", "https://user@github.com/a", "https://github.com:444/a", "file:///C:/evil" })
            Check(!GitHubUpdateClient.AllowedHost(new Uri(url)), "拒绝非受信任地址");
        Reject(() => UpdateJobs.DirectoryFor("../escape"));
        var args = UpdateJobs.SetupStart(@"D:\测试 空格\package.exe", @"D:\我的 PTBox", @"D:\日志\setup.log").ArgumentList;
        Check(args.Contains("/NORESTART") && args.Contains("/NOCLOSEAPPLICATIONS") && args.Contains(@"/DIR=D:\我的 PTBox"), "安装参数不能被拆开或强制关程序");
    }
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Requests.Add(request.RequestUri!.ToString()); return Task.FromResult(respond(request)); }
    }
    public static async Task NetworkAsync(string root)
    {
        using var key = RSA.Create(2048); var package = Encoding.UTF8.GetBytes("installer fixture, never executed");
        var candidate = Candidate(key, package); var trust = new ReleaseTrust("NeuronXL/PTBox", new() { ["test"] = key.ExportSubjectPublicKeyInfoPem() });
        var m = candidate.Manifest;
        byte[] Listing(bool prerelease = false, string version = "1.2.0") => JsonSerializer.SerializeToUtf8Bytes(new
        {
            tag_name = "v" + version, draft = false, prerelease, body = "更新说明\n<script>must remain text</script>",
            assets = new[] { "update.json", "update.json.sig", m.AssetName }.Select(name => new { name, browser_download_url = $"https://github.com/NeuronXL/PTBox/releases/download/v1.2.0/{name}" })
        });
        static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        var mode = "ok"; bool sentCache = false;
        var handler = new FakeHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/repos/NeuronXL/PTBox") return Bytes(Encoding.UTF8.GetBytes("{}"));
            if (path.EndsWith("/latest"))
            {
                if (mode == "404") return new(HttpStatusCode.NotFound);
                if (mode == "rate") return new((HttpStatusCode)429);
                if (mode == "network") throw new HttpRequestException("offline");
                if (mode == "cached") { sentCache = request.Headers.IfNoneMatch.Any(); return new(HttpStatusCode.NotModified); }
                var response = Bytes(Listing(mode == "prerelease", mode == "old" ? "1.0.0" : "1.2.0"));
                response.Headers.ETag = new("\"release-test\""); return response;
            }
            if (path.EndsWith("/update.json")) return Bytes(candidate.ManifestBytes);
            if (path.EndsWith("/update.json.sig")) return Bytes(mode == "signature" ? new byte[256] : candidate.Signature);
            if (mode == "redirect") return new(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://evil.example/package") } };
            if (mode == "oversize") return Bytes(new byte[package.Length + 1]);
            if (mode == "hash") return Bytes(new byte[package.Length]);
            return Bytes(package);
        });
        using var client = new GitHubUpdateClient(trust, Path.Combine(root, "update-network", "cache.json"), handler);
        var found = await client.CheckAsync("1.1.0", default); Check(found?.Manifest.Version == "1.2.0", "发现正式更新");
        mode = "cached"; found = await client.CheckAsync("1.1.0", default); Check(sentCache && found != null, "304 复用元数据仍重新验签");
        mode = "signature"; await RejectAsync(async () => await client.CheckAsync("1.1.0", default));
        foreach (var state in new[] { "old", "prerelease", "404" }) { mode = state; Check(await client.CheckAsync("1.1.0", default) == null, "跳过无新版和测试版"); }
        foreach (var state in new[] { "rate", "network" }) { mode = state; await RejectAsync(async () => await client.CheckAsync("1.1.0", default)); }
        foreach (var state in new[] { "redirect", "oversize", "hash" })
        {
            mode = state; var directory = Path.Combine(root, "download-" + state);
            await RejectAsync(() => client.DownloadAsync(candidate, directory, null, default));
            Check(!File.Exists(Path.Combine(directory, "package.exe")) && Directory.GetFiles(directory, "*.part").Length == 0, "坏包不可就绪，清理临时文件");
        }
        mode = "ok"; var download = Path.Combine(root, "download-valid");
        await client.DownloadAsync(candidate, download, null, default);
        await using (var locked = await UpdateProtocol.OpenVerifiedPackageAsync(Path.Combine(download, "package.exe"), m))
        {
            try { File.WriteAllText(locked.Name, "tamper"); throw new Exception("Verified package was writable"); } catch (IOException) { }
        }
        File.WriteAllText(Path.Combine(download, "package.exe"), "tampered");
        await client.DownloadAsync(candidate, download, null, default);
        Check(File.ReadAllBytes(Path.Combine(download, "package.exe")).SequenceEqual(package), "损坏缓存应重新下载");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await RejectAsync(() => client.DownloadAsync(candidate, Path.Combine(root, "cancel-download"), null, cancelled.Token));
        Check(!handler.Requests.Any(x => x.Contains("evil.example")), "不应发送不受信任的重定向请求");
    }
    public static async Task FailureReportingAsync(string root)
    {
        var resetAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds());
        HttpResponseMessage Error(int status, string body = "{}", params (string Name, string Value)[] headers)
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
            foreach (var (name, value) in headers) response.Headers.TryAddWithoutValidation(name, value);
            return response;
        }
        async Task<GitHubRequestException> Failure(string name, Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            using var client = new GitHubUpdateClient(ReleaseTrust.Embedded(), Path.Combine(root, name, "cache.json"), new FakeHandler(respond));
            try { await client.CheckAsync("1.1.0", default); }
            catch (GitHubRequestException ex) { return ex; }
            throw new Exception("Expected GitHub error: " + name);
        }
        var denied = await Failure("denied", _ => Error(403, "<html>Access denied</html>", ("X-GitHub-Request-Id", "fixture-403")));
        Check(!denied.IsRateLimited && denied.RetryAt == null && denied.Message.Contains("未确认是限流") && !denied.Message.Contains("<html>"), "普通 403 不能误报限流或显示 HTML");
        Check(denied.StatusCode == HttpStatusCode.Forbidden && denied.Diagnostic.Contains("fixture-403"), "保留状态码和请求标识用于诊断");
        var primary = await Failure("primary", _ => Error(403, "{}", ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", resetAt.ToUnixTimeSeconds().ToString())));
        Check(primary.IsRateLimited && primary.RetryAt > resetAt && primary.Message.Contains("后重试"), "主限流按服务器重置时间等待");
        var secondary = await Failure("secondary", _ => Error(403, "{\"message\":\"You have exceeded a secondary rate limit.\"}", ("Retry-After", "120")));
        Check(secondary.IsRateLimited && secondary.RetryAt > DateTimeOffset.UtcNow.AddSeconds(110), "次级限流遵守 Retry-After");
        var bodyOnly = await Failure("body-only", _ => Error(403, "{\"message\":\"API rate limit exceeded\"}"));
        Check(bodyOnly.IsRateLimited && bodyOnly.RetryAt > DateTimeOffset.UtcNow.AddSeconds(50), "仅正文说明限流时至少冷却一分钟");
        var dateRetry = DateTimeOffset.UtcNow.AddMinutes(3);
        var dated = await Failure("date-retry", _ => Error(429, "{}", ("Retry-After", dateRetry.ToString("R"))));
        Check(dated.RetryAt > dateRetry.AddSeconds(-1), "支持 HTTP 日期形式 Retry-After");
        var malformed = await Failure("malformed", _ => Error(429, new string('x', 10000), ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", "999999999999999"), ("Retry-After", "invalid")));
        Check(malformed.IsRateLimited && malformed.RetryAt > DateTimeOffset.UtcNow.AddSeconds(50), "非法响应头及超长非 JSON 正文仍能识别 429 并使用安全冷却");
        var server = await Failure("server", _ => Error(503));
        Check(!server.IsRateLimited && server.Message.Contains("服务暂时不可用"), "服务错误不能误报限流");
        var missing = await Failure("missing-repository", _ => Error(404));
        Check(missing.Message.Contains("不可访问"), "不可访问的仓库不能误报暂无正式版本");

        var directory = Path.Combine(root, "failure-service");
        var handler = new FakeHandler(_ => Error(403, "{}", ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", resetAt.ToUnixTimeSeconds().ToString())));
        using (var service = new UpdateService(directory, handler))
        {
            await service.CheckAsync(); await service.CheckAsync();
            Check(handler.Requests.Count == 1 && service.Status.Contains("冷却") && !service.CanDownload && !service.CanInstall, "限流后重复点击不再请求网络");
            Check(File.ReadAllText(Path.Combine(directory, "Logs", "launcher.log")).Contains("X-RateLimit-Remaining=0"), "日志包含响应诊断信息");
        }
        var reopenedHandler = new FakeHandler(_ => throw new Exception("Cooldown must survive restart"));
        using (var reopened = new UpdateService(directory, reopenedHandler))
        { await reopened.CheckAsync(); Check(reopenedHandler.Requests.Count == 0 && reopened.Status.Contains("冷却"), "冷却时间跨重启保存"); }
        UpdateProtocol.WriteJson(Path.Combine(directory, "Updates", "preferences.json"), new UpdatePreferences { RetryNotBefore = DateTimeOffset.UtcNow.AddSeconds(-1) });
        var noReleaseHandler = new FakeHandler(request => Error(request.RequestUri!.AbsolutePath.EndsWith("/latest") ? 404 : 200));
        using (var noRelease = new UpdateService(directory, noReleaseHandler))
        {
            await noRelease.CheckAsync();
            Check(noReleaseHandler.Requests.Count == 2 && noRelease.Latest == "暂无正式版本" && noRelease.Status.Contains("仓库可访问") && !noRelease.CanDownload, "冷却过期可重试，无 Release 和当前最新版分别显示");
        }
        var deniedHandler = new FakeHandler(_ => Error(403));
        using (var deniedService = new UpdateService(Path.Combine(root, "denied-service"), deniedHandler))
        { await deniedService.CheckAsync(); await deniedService.CheckAsync(); Check(deniedHandler.Requests.Count == 2, "普通拒绝不触发限流冷却，修正网络后可立即重试"); }
        using var offline = new UpdateService(Path.Combine(root, "offline-service"), new FakeHandler(_ => throw new HttpRequestException("socket failure")));
        await offline.CheckAsync(); Check(offline.Status.Contains("网络或代理") && offline.Latest == "检查未完成", "断网显示可操作提示");
    }
    public static void Migration(string root)
    {
        var old = Path.Combine(root, "migration-old"); var target = Path.Combine(root, "migration-user");
        Directory.CreateDirectory(Path.Combine(old, "images")); File.WriteAllText(Path.Combine(old, "images", "wall.png"), "fixture");
        var config = new LauncherConfig { Background = "images/wall.png", Theme = "Charcoal", Apps = [new() { Id = "custom", Name = "我的应用", Icon = "images/wall.png", Path = "tools/app.exe", WorkingDirectory = "tools", RunAsAdministrator = true }] };
        var legacy = new ConfigService(old, new(old)); legacy.Save(config); var original = File.ReadAllBytes(legacy.ConfigPath);
        Check(!DataMigrationService.IsInstalled(old), "无标记为便携模式"); File.WriteAllText(Path.Combine(old, "ptbox.install"), DataMigrationService.AppId);
        Check(DataMigrationService.IsInstalled(old), "明确安装标记");
        DataMigrationService.Migrate(old, target, (_, _) => throw new Exception("fresh migration should not conflict"));
        var migrated = new ConfigService(target, new(target)).Load(); Check(migrated.Apps.Single().Name == "我的应用" && migrated.Theme == "Charcoal", "配置完整迁移");
        Check(File.Exists(migrated.Background) && migrated.Background.StartsWith(target) && Path.IsPathFullyQualified(migrated.Apps[0].Path), "相对资源和程序路径保留语义");
        Check(migrated.Apps[0].WorkingDirectory == Path.Combine(old,"tools") && migrated.Apps[0].RunAsAdministrator, "迁移保留快捷方式的工作目录及管理员标志");
        Check(original.SequenceEqual(File.ReadAllBytes(legacy.ConfigPath)), "原始配置保持原样");
        migrated.Apps[0].Name = "迁移后修改"; new ConfigService(target, new(target)).Save(migrated);
        DataMigrationService.Migrate(old, target, (_, _) => true);
        Check(new ConfigService(target, new(target)).Load().Apps[0].Name == "迁移后修改", "重复迁移不能覆盖新配置");
        var conflict = Path.Combine(root, "migration-conflict"); var existing = new ConfigService(conflict, new(conflict)); existing.Save(new());
        bool asked = false; DataMigrationService.Migrate(old, conflict, (_, _) => { asked = true; return false; });
        Check(asked && existing.Load().Apps.Count == 0, "冲突允许明确保留现有配置");
        var conflictImport = Path.Combine(root, "migration-import"); new ConfigService(conflictImport, new(conflictImport)).Save(new());
        DataMigrationService.Migrate(old, conflictImport, (_, _) => true);
        Check(new ConfigService(conflictImport, new(conflictImport)).Load().Apps.Count == 1 && Directory.GetFiles(Path.Combine(conflictImport, "Backups")).Length == 2, "冲突导入保留两份备份");
        var future = new ConfigService(Path.Combine(root, "future-format"), new(root)); Directory.CreateDirectory(Path.GetDirectoryName(future.ConfigPath)!);
        const string futureJson = "{\"version\":2,\"apps\":[],\"futureData\":\"keep\"}"; File.WriteAllText(future.ConfigPath, futureJson);
        future.Load(); Check(future.IsReadOnly && future.LoadWarning != null, "未来格式只读保护");
        Reject(() => future.Save(new())); Check(File.ReadAllText(future.ConfigPath) == futureJson, "未来配置不能重置或写回");
        File.WriteAllText(Path.Combine(old, "ptbox.portable"), ""); Check(!DataMigrationService.IsInstalled(old), "显式便携标记优先");
    }
    private sealed class DelayedHandler : HttpMessageHandler
    {
        public int Count;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Count++; await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.NotFound); }
    }
    public static async Task SingleFlightAsync(string root)
    {
        var handler = new DelayedHandler(); using var service = new UpdateService(Path.Combine(root, "single-update"), handler);
        var first = service.CheckAsync();
        Check(service.Busy && !service.CanCheck && !service.CanDownload && !service.CanInstall, "检查期间禁用重复操作");
        await service.CheckAsync(); await service.DownloadAsync(); Check(handler.Count == 1, "重复点击不能发起第二个请求");
        service.Cancel(); await first; Check(!service.Busy && service.CanCheck && service.Status.Contains("取消"), "取消后可重试");
        var second = service.CheckAsync(); Check(handler.Count == 2, "允许主动重试"); service.Cancel(); await second;
        service.Automatic = false;
        using var reopened = new UpdateService(Path.Combine(root, "single-update"), new DelayedHandler());
        Check(!reopened.Automatic && reopened.LastChecked != "尚未检查", "更新偏好与检查时间持久化");
    }
    public static UpdateService PreviewService(string root)
    {
        using var key = RSA.Create(2048); var package = Encoding.UTF8.GetBytes("not an executable");
        var candidate = Candidate(key, package); var trust = new ReleaseTrust("NeuronXL/PTBox", new() { ["test"] = key.ExportSubjectPublicKeyInfoPem() });
        var handler = new FakeHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            byte[] bytes;
            if (path.EndsWith("/latest")) bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                tag_name = "v1.2.0", draft = false, prerelease = false,
                body = "PTBox 1.2.0\n\n• 改善大屏导航体验。\n• 优化应用图标加载。\n• 更新会保留你的应用和设置。\n\n这是本地模拟更新，用于验证界面和下载流程。",
                assets = new[] { "update.json", "update.json.sig", candidate.Manifest.AssetName }.Select(name => new { name, browser_download_url = "https://github.com/NeuronXL/PTBox/releases/download/v1.2.0/" + name })
            });
            else if (path.EndsWith("/update.json")) bytes = candidate.ManifestBytes;
            else if (path.EndsWith("/update.json.sig")) bytes = candidate.Signature;
            else bytes = package;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        });
        return new UpdateService(Path.Combine(root, "preview-updates"), handler, trust);
    }
    public static async Task ParentIdentityAsync(string fixture, string root)
    {
        var installation = Path.Combine(root, "parent-identity"); Directory.CreateDirectory(installation);
        var fixtureDirectory = Path.GetDirectoryName(Path.GetFullPath(fixture))!;
        foreach (var file in Directory.GetFiles(fixtureDirectory)) File.Copy(file, Path.Combine(installation, Path.GetFileName(file)));
        var executable = Path.Combine(installation, "PTBox.Launcher.exe"); File.Copy(Path.GetFullPath(fixture), executable);
        var ready = Path.Combine(installation, "ready"); var close = Path.Combine(installation, "close");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "--lifetime", "15000", "--ready-file", ready, "--close-file", close }) info.ArgumentList.Add(arg);
        using var parent = Process.Start(info)!;
        try
        {
            var timer = Stopwatch.StartNew();
            while (!File.Exists(ready) && timer.Elapsed < TimeSpan.FromSeconds(8)) await Task.Delay(100);
            Check(File.Exists(ready), "父进程测试实例已启动");
            var job = new UpdateJob(Guid.NewGuid().ToString("N"), installation, parent.Id, parent.StartTime.ToUniversalTime().Ticks, UpdateProtocol.AppVersion);
            using (var verified = UpdateJobs.ValidateParent(job)) Check(verified.Id == parent.Id, "按路径、时间与版本验证真实父进程");
            Reject(() => UpdateJobs.ValidateParent(job with { ParentStartTicks = job.ParentStartTicks - 1 }));
            Reject(() => UpdateJobs.ValidateParent(job with { CurrentVersion = "0.0.1" }));
            Reject(() => UpdateJobs.ValidateParent(job with { InstallDirectory = root }));
            File.WriteAllText(close, "close"); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { if (!parent.HasExited) { File.WriteAllText(close, "close"); if (!parent.WaitForExit(3000)) parent.Kill(); } }
    }
}
