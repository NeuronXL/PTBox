using System.Globalization;
using System.Net;
using System.Text.Json;

namespace PTBox.UpdateCore;

public sealed class GitHubRequestException : HttpRequestException
{
    public bool IsRateLimited { get; }
    public DateTimeOffset? RetryAt { get; }
    public string Diagnostic { get; }

    private GitHubRequestException(string message, HttpStatusCode status, bool limited, DateTimeOffset? retryAt, string diagnostic)
        : base(message, null, status) { IsRateLimited = limited; RetryAt = retryAt; Diagnostic = diagnostic; }

    internal static async Task<GitHubRequestException> FromResponseAsync(HttpResponseMessage response, Uri uri, CancellationToken token)
    {
        string Header(string name)
        {
            var value = response.Headers.TryGetValues(name, out var values) ? string.Join(",", values).Replace('\r', ' ').Replace('\n', ' ') : "";
            return value[..Math.Min(256, value.Length)];
        }
        // Error pages may be HTML or very large. Read only a bounded prefix, never display raw server content.
        var buffer = new byte[4096]; var count = 0;
        await using (var stream = await response.Content.ReadAsStreamAsync(token))
        {
            while (count < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(count), token);
                if (read == 0) break;
                count += read;
            }
        }
        var serverMessage = "";
        try
        {
            using var doc = JsonDocument.Parse(buffer.AsMemory(0, count));
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                serverMessage = message.GetString() ?? "";
        }
        catch (JsonException) { }
        var remaining = Header("X-RateLimit-Remaining");
        var reset = Header("X-RateLimit-Reset");
        var retry = Header("Retry-After");
        var status = (int)response.StatusCode;
        var limited = status == 429 || status == 403 && (remaining == "0" || retry.Length > 0 ||
            serverMessage.Contains("rate limit", StringComparison.OrdinalIgnoreCase) || serverMessage.Contains("abuse detection", StringComparison.OrdinalIgnoreCase));
        DateTimeOffset? retryAt = null;
        if (limited)
        {
            var now = DateTimeOffset.UtcNow;
            retryAt = now.AddMinutes(1);
            if (long.TryParse(retry, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds <= 31536000)
                retryAt = now.AddSeconds(Math.Max(1, seconds));
            else if (DateTimeOffset.TryParse(retry, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var retryDate) && retryDate > now)
                retryAt = retryDate;
            if (remaining == "0" && long.TryParse(reset, out var epoch) && epoch is >= 0 and <= 253402300799)
            {
                var resetAt = DateTimeOffset.FromUnixTimeSeconds(epoch).AddSeconds(epoch < 253402300799 ? 1 : 0);
                if (resetAt > retryAt) retryAt = resetAt;
            }
        }
        var text = limited ? $"GitHub 请求已限流（HTTP {status}），请在 {retryAt!.Value.ToLocalTime():MM-dd HH:mm:ss} 后重试。" : status switch
        {
            403 => "GitHub 访问被拒绝（HTTP 403），未确认是限流。请检查网络或代理，并尝试打开 GitHub Releases。",
            401 => "GitHub 拒绝了匿名访问（HTTP 401），请确认发布仓库公开可访问。",
            404 => "GitHub 仓库或发行附件不可访问（HTTP 404），请检查仓库和发布附件。",
            >= 500 => $"GitHub 服务暂时不可用（HTTP {status}），请稍后重试。",
            _ => $"GitHub 请求失败（HTTP {status}），请尝试打开 GitHub Releases。"
        };
        var diagnostic = $"Host={uri.Host}; Path={uri.AbsolutePath}; HTTP={status}; X-RateLimit-Remaining={remaining}; X-RateLimit-Reset={reset}; Retry-After={retry}; X-GitHub-Request-Id={Header("X-GitHub-Request-Id")}; RateLimited={limited}";
        return new(text, response.StatusCode, limited, retryAt, diagnostic);
    }
}
