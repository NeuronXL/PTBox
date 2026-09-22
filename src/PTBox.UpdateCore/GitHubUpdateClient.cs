using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PTBox.UpdateCore;

public sealed class GitHubUpdateClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ReleaseTrust _trust;
    private readonly string _cache;
    public bool HasPublishedRelease { get; private set; }
    public GitHubUpdateClient(ReleaseTrust trust, string cache, HttpMessageHandler? handler = null)
    {
        _trust = trust; _cache = cache;
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PTBox/" + UpdateProtocol.AppVersion);
    }
    public static bool AllowedHost(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        uri.Host is "api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";

    private async Task<HttpResponseMessage> GetAsync(Uri uri, string? etag, CancellationToken token)
    {
        for (var count = 0; count < 6; count++)
        {
            if (!AllowedHost(uri)) throw new InvalidDataException("下载地址不属于受支持的 GitHub HTTPS 主机。");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (uri.Host == "api.github.com")
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                if (etag != null) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location; response.Dispose();
                if (location == null) throw new InvalidDataException("下载重定向地址为空。");
                uri = new Uri(uri, location); continue;
            }
            return response;
        }
        throw new InvalidDataException("下载重定向次数过多。");
    }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, Uri uri, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode) throw await GitHubRequestException.FromResponseAsync(response, uri, token);
    }
    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken token)
    {
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("服务器响应超过大小限制。");
        await using var input = await response.Content.ReadAsStreamAsync(token); using var output = new MemoryStream();
        var buffer = new byte[8192]; int n;
        while ((n = await input.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + n > limit) throw new InvalidDataException("服务器响应超过大小限制。");
            output.Write(buffer, 0, n);
        }
        return output.ToArray();
    }
    private sealed record ReleaseCache(string Repository, string? Etag, byte[] Body);
    public async Task<ReleaseCandidate?> CheckAsync(string currentVersion, CancellationToken token)
    {
        HasPublishedRelease = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(40)); token = deadline.Token;
        ReleaseCache? cache = null;
        try { if (File.Exists(_cache) && new FileInfo(_cache).Length < 2_000_000) cache = JsonSerializer.Deserialize<ReleaseCache>(File.ReadAllBytes(_cache), UpdateProtocol.Json); }
        catch (Exception ex) when (ex is IOException or JsonException) { }
        if (cache?.Repository != _trust.Repository) cache = null;
        var latestUri = new Uri($"https://api.github.com/repos/{_trust.Repository}/releases/latest");
        using var response = await GetAsync(latestUri, cache?.Etag, token);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // The latest endpoint also returns 404 for a missing/private repository.
            var repositoryUri = new Uri($"https://api.github.com/repos/{_trust.Repository}");
            using var repository = await GetAsync(repositoryUri, null, token);
            await EnsureSuccessAsync(repository, repositoryUri, token);
            return null;
        }
        if (response.StatusCode != HttpStatusCode.NotModified || cache == null) await EnsureSuccessAsync(response, latestUri, token);
        var bytes = response.StatusCode == HttpStatusCode.NotModified && cache != null ? cache.Body : await ReadLimitedAsync(response, 1_000_000, token);
        if (bytes.Length > 1_000_000) throw new InvalidDataException("版本信息过大。");
        using var doc = JsonDocument.Parse(bytes); var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        HasPublishedRelease = true;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith('v')) throw new InvalidDataException("Release 标签必须以 v 开头。");
        if (UpdateProtocol.ParseVersion(tag[1..]) <= UpdateProtocol.ParseVersion(currentVersion)) return null;
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        string AssetUrl(string name)
        {
            var matches = assets.Where(x => x.GetProperty("name").GetString() == name).ToArray();
            var expected = $"https://github.com/{_trust.Repository}/releases/download/{tag}/{name}";
            if (matches.Length != 1 || matches[0].GetProperty("browser_download_url").GetString() != expected)
                throw new InvalidDataException($"发行附件缺失或地址无效：{name}");
            return expected;
        }
        using var manifestResponse = await GetAsync(new Uri(AssetUrl("update.json")), null, token);
        await EnsureSuccessAsync(manifestResponse, new Uri(AssetUrl("update.json")), token);
        var manifestBytes = await ReadLimitedAsync(manifestResponse, 65536, token);
        using var signatureResponse = await GetAsync(new Uri(AssetUrl("update.json.sig")), null, token);
        await EnsureSuccessAsync(signatureResponse, new Uri(AssetUrl("update.json.sig")), token);
        var signature = await ReadLimitedAsync(signatureResponse, 1024, token);
        var manifest = UpdateProtocol.VerifyManifest(manifestBytes, signature, _trust);
        if (tag != "v" + manifest.Version || AssetUrl(manifest.AssetName) != manifest.DownloadUrl) throw new InvalidDataException("发行版本不一致。");
        UpdateProtocol.CheckCompatibility(manifest, currentVersion, Environment.OSVersion.Version.Build);
        try { UpdateProtocol.WriteJson(_cache, new ReleaseCache(_trust.Repository, response.Headers.ETag?.ToString() ?? cache?.Etag, bytes)); }
        catch (IOException) { /* Optional cache must not prevent updating. */ }
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        return new(manifest, manifestBytes, signature, notes.Length > 12000 ? notes[..12000] : notes);
    }
    public async Task DownloadAsync(ReleaseCandidate candidate, string directory, IProgress<double>? progress, CancellationToken token)
    {
        var m = UpdateProtocol.VerifyManifest(candidate.ManifestBytes, candidate.Signature, _trust);
        Directory.CreateDirectory(directory);
        var package = Path.Combine(directory, "package.exe");
        if (File.Exists(package))
        {
            try
            {
                await using var existing = await UpdateProtocol.OpenVerifiedPackageAsync(package, m, token);
                await File.WriteAllBytesAsync(Path.Combine(directory, "update.json"), candidate.ManifestBytes, token);
                await File.WriteAllBytesAsync(Path.Combine(directory, "update.json.sig"), candidate.Signature, token);
                progress?.Report(100); return;
            }
            catch (InvalidDataException) { File.Delete(package); }
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(30)); token = deadline.Token;
        using var response = await GetAsync(new Uri(m.DownloadUrl), null, token);
        await EnsureSuccessAsync(response, new Uri(m.DownloadUrl), token);
        if (response.Content.Headers.ContentLength is long length && length != m.Size) throw new InvalidDataException("下载大小与清单不一致。");
        var part = package + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            await using (var output = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            {
                var buffer = new byte[81920]; long total = 0; int n;
                while (true)
                {
                    using var idle = CancellationTokenSource.CreateLinkedTokenSource(token); idle.CancelAfter(TimeSpan.FromSeconds(45));
                    n = await input.ReadAsync(buffer, idle.Token); if (n == 0) break;
                    total += n; if (total > m.Size) throw new InvalidDataException("下载超出清单大小。");
                    await output.WriteAsync(buffer.AsMemory(0, n), token); progress?.Report(total * 100d / m.Size);
                }
                await output.FlushAsync(token);
            }
            await using (var verified = await UpdateProtocol.OpenVerifiedPackageAsync(part, m, token)) { }
            await File.WriteAllBytesAsync(Path.Combine(directory, "update.json"), candidate.ManifestBytes, token);
            await File.WriteAllBytesAsync(Path.Combine(directory, "update.json.sig"), candidate.Signature, token);
            File.Move(part, package, true);
        }
        finally { if (File.Exists(part)) File.Delete(part); }
    }
    public void Dispose() => _http.Dispose();
}
