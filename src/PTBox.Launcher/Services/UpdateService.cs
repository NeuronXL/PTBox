using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using PTBox.Launcher.ViewModels;
using PTBox.UpdateCore;

namespace PTBox.Launcher.Services;

public sealed class UpdatePreferences
{
    public bool Automatic { get; set; } = true;
    public DateTimeOffset? LastAttempt { get; set; }
    public DateTimeOffset? RetryNotBefore { get; set; }
}

public sealed class UpdateService : ObservableObject, IDisposable
{
    private readonly GitHubUpdateClient _client;
    private readonly string _root, _preferencesPath;
    private readonly LoggingService _log;
    private UpdatePreferences _preferences = new();
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private ReleaseCandidate? _candidate;
    private bool _busy, _ready;
    private string _status = "可检查 GitHub 上的正式版本。", _notes = "", _latest = "尚未检查";
    private double _progress;
    public string CurrentVersion => UpdateProtocol.AppVersion;
    public string RepositoryUrl => "https://github.com/" + ReleaseTrust.Embedded().Repository + "/releases";
    public string InstallMode { get; }
    public bool CanInstallHere { get; }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Notes { get => _notes; private set => Set(ref _notes, value); }
    public string Latest { get => _latest; private set => Set(ref _latest, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool Busy { get => _busy; private set { Set(ref _busy, value); RefreshActions(); } }
    public bool CanCheck => !Busy;
    public bool CanDownload => !Busy && _candidate != null && !_ready;
    public bool CanInstall => !Busy && _ready && CanInstallHere;
    public string Size => _candidate == null ? "" : FormattableString.Invariant($"完整安装包 · {_candidate.Manifest.Size / 1048576d:F1} MiB");
    public string LastChecked => _preferences.LastAttempt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "尚未检查";
    public bool Automatic
    {
        get => _preferences.Automatic;
        set
        {
            var previous = _preferences.Automatic; _preferences.Automatic = value;
            try { PersistPreferences(); }
            catch (Exception ex) { _preferences.Automatic = previous; Status = "无法保存更新偏好：" + ex.Message; }
            Notify();
        }
    }
    public UpdateService(string dataDirectory, HttpMessageHandler? handler = null, ReleaseTrust? trust = null)
    {
        _log = new(dataDirectory);
        _root = Path.Combine(dataDirectory, "Updates"); _preferencesPath = Path.Combine(_root, "preferences.json");
        try { if (File.Exists(_preferencesPath)) _preferences = JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllBytes(_preferencesPath), UpdateProtocol.Json) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { Status = "更新偏好无法读取，使用默认值。"; }
        _client = new(trust ?? ReleaseTrust.Embedded(), Path.Combine(_root, "release-cache.json"), handler);
        try { UpdateJobs.ValidateInstallation(AppContext.BaseDirectory); CanInstallHere = true; InstallMode = "安装版 · 支持应用内升级"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { InstallMode = "便携版或未注册安装 · 请下载后手动安装接入版"; }
    }
    private void PersistPreferences() => UpdateProtocol.WriteJson(_preferencesPath, _preferences);
    private void RefreshActions() { Notify(nameof(CanCheck)); Notify(nameof(CanDownload)); Notify(nameof(CanInstall)); Notify(nameof(Size)); }
    public async Task BackgroundCheckAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), _lifetime.Token);
            if (Automatic && (_preferences.LastAttempt == null || DateTimeOffset.UtcNow - _preferences.LastAttempt >= TimeSpan.FromDays(1))) await CheckAsync();
        }
        catch (OperationCanceledException) { }
    }
    public async Task CheckAsync()
    {
        if (Busy || IsCoolingDown()) return; Busy = true; _ready = false; _candidate = null; Notes = ""; Progress = 0; Latest = "正在检查";
        _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        try
        {
            _preferences.LastAttempt = DateTimeOffset.UtcNow; PersistPreferences(); Notify(nameof(LastChecked));
            Status = "正在检查正式版本…";
            _candidate = await _client.CheckAsync(CurrentVersion, _operation.Token);
            Latest = _candidate?.Manifest.Version ?? (_client.HasPublishedRelease ? CurrentVersion : "暂无正式版本");
            Notes = _candidate?.Notes ?? (_client.HasPublishedRelease ? "当前没有可用的更新。" : "发布仓库尚无正式版本。发布后可再次检查更新。");
            Status = _candidate != null ? "发现新版本，可下载后选择安装时间。" : _client.HasPublishedRelease ? "当前已是最新可用版本。" : "仓库可访问，暂无正式版本。";
        }
        catch (OperationCanceledException) { Status = _operation.IsCancellationRequested ? "检查已取消。" : "检查超时，请重试。"; Latest = "检查未完成"; }
        catch (Exception ex) { ReportFailure("检查", ex); Latest = "检查未完成"; }
        finally { _operation.Dispose(); _operation = null; Busy = false; }
    }
    private string DownloadDirectory => Path.Combine(_root, "downloads", _candidate!.Manifest.Version);
    public async Task DownloadAsync()
    {
        if (!CanDownload || IsCoolingDown()) return; Busy = true; _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        try
        {
            Status = "正在下载安装包…";
            await _client.DownloadAsync(_candidate!, DownloadDirectory, new Progress<double>(value => Progress = value), _operation.Token);
            _ready = true; Status = CanInstallHere ? "下载和校验完成，可以重启 PTBox 更新。" : "下载和校验完成。请打开下载目录，手动安装接入版。";
        }
        catch (OperationCanceledException) { Status = _operation.IsCancellationRequested ? "下载已取消，可重新下载。" : "下载超时，可重试。"; }
        catch (Exception ex) { ReportFailure("下载", ex); }
        finally { _operation.Dispose(); _operation = null; Busy = false; }
    }
    private bool IsCoolingDown()
    {
        if (_preferences.RetryNotBefore is not { } retryAt || retryAt <= DateTimeOffset.UtcNow) return false;
        Status = $"GitHub 请求仍在冷却，请在 {retryAt.ToLocalTime():MM-dd HH:mm:ss} 后重试。";
        return true;
    }
    private void ReportFailure(string operation, Exception ex)
    {
        if (ex is GitHubRequestException github)
        {
            _log.Error($"更新{operation}失败：{github.Diagnostic}", ex);
            if (github.IsRateLimited)
            {
                _preferences.RetryNotBefore = github.RetryAt;
                try { PersistPreferences(); }
                catch (Exception saveError) when (saveError is IOException or UnauthorizedAccessException) { _log.Error("保存更新冷却时间失败", saveError); }
            }
        }
        else _log.Error($"更新{operation}失败", ex);
        var message = ex is HttpRequestException and not GitHubRequestException
            ? "无法连接 GitHub，请检查网络或代理后重试。" : ex.Message;
        Status = operation + "失败：" + message;
    }
    public void Cancel() => _operation?.Cancel();
    public void OpenReleases() => Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });
    public void OpenDownloads()
    {
        var directory = Path.Combine(_root, "downloads"); Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { directory } });
    }
    public async Task<string> PrepareInstallAsync()
    {
        if (!CanInstall) throw new InvalidOperationException("当前更新尚未就绪。");
        Busy = true; string? folder = null;
        try
        {
            UpdateJobs.ValidateInstallation(AppContext.BaseDirectory);
            await using (var package = await UpdateProtocol.OpenVerifiedPackageAsync(Path.Combine(DownloadDirectory, "package.exe"), _candidate!.Manifest)) { }
            var id = Guid.NewGuid().ToString("N"); folder = UpdateJobs.DirectoryFor(id);
            UpdateJobs.NoReparsePoints(folder);
            var runnerSource = Path.Combine(AppContext.BaseDirectory, "Updater");
            if (!File.Exists(Path.Combine(runnerSource, "PTBox.Updater.exe"))) throw new FileNotFoundException("缺少独立更新程序，请手动安装接入版。");
            Directory.CreateDirectory(folder);
            await Task.Run(() =>
            {
                foreach (var file in Directory.EnumerateFiles(runnerSource, "*", SearchOption.AllDirectories))
                {
                    UpdateJobs.NoReparsePoints(Path.GetDirectoryName(file)!);
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("更新程序目录包含链接文件。");
                    var destination = Path.Combine(folder, "runner", Path.GetRelativePath(runnerSource, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination);
                }
                foreach (var file in new[] { "package.exe", "update.json", "update.json.sig" }) File.Copy(Path.Combine(DownloadDirectory, file), Path.Combine(folder, file));
            });
            using var current = Process.GetCurrentProcess();
            UpdateProtocol.WriteJson(Path.Combine(folder, "job.json"), new UpdateJob(id, AppContext.BaseDirectory.TrimEnd('\\'), current.Id, current.StartTime.ToUniversalTime().Ticks, CurrentVersion));
            var info = new ProcessStartInfo(Path.Combine(folder, "runner", "PTBox.Updater.exe")) { UseShellExecute = false, WorkingDirectory = folder };
            info.ArgumentList.Add(id); using var updater = Process.Start(info) ?? throw new IOException("无法启动更新程序。");
            var timer = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(folder, "ready")))
            {
                if (updater.HasExited || timer.Elapsed > TimeSpan.FromSeconds(40)) throw new IOException("更新程序未就绪，当前 PTBox 将保持运行。请查看更新程序提示。");
                await Task.Delay(200);
            }
            return id;
        }
        catch { if (folder != null && Directory.Exists(folder)) File.WriteAllText(Path.Combine(folder, "cancel"), "cancel"); throw; }
        finally { Busy = false; }
    }
    public void Dispose() { _lifetime.Cancel(); _operation?.Cancel(); _client.Dispose(); _lifetime.Dispose(); }
}
