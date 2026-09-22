using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PTBox.UpdateCore;

public sealed record ReleaseTrust(string Repository, Dictionary<string, string> Keys)
{
    public static ReleaseTrust Embedded()
    {
        using var stream = typeof(ReleaseTrust).Assembly.GetManifestResourceStream("PTBox.UpdateCore.ReleaseTrust.json")!;
        return JsonSerializer.Deserialize<ReleaseTrust>(stream, UpdateProtocol.Json)!;
    }
}

public sealed record UpdateManifest(int SchemaVersion, string Version, string Channel, string Rid,
    int MinWindowsBuild, string MinUpdaterVersion, string AssetName, string DownloadUrl,
    long Size, string Sha256, DateTimeOffset PublishedAt, string KeyId);

public sealed record ReleaseCandidate(UpdateManifest Manifest, byte[] ManifestBytes, byte[] Signature, string Notes);

public static partial class UpdateProtocol
{
    public const string UpdaterVersion = "1.0.0";
    public const long MaxPackageSize = 512L * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static string AppVersion => typeof(UpdateProtocol).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion.Split('+')[0];

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
    public static Version ParseVersion(string value) => value is not null && VersionPattern().IsMatch(value) && Version.TryParse(value, out var v)
        ? v : throw new InvalidDataException("版本号必须是三段数字。");

    public static UpdateManifest VerifyManifest(byte[] bytes, byte[] signature, ReleaseTrust trust)
    {
        if (bytes.Length is 0 or > 65536 || signature.Length is 0 or > 1024) throw new InvalidDataException("更新清单大小不正确。");
        var m = JsonSerializer.Deserialize<UpdateManifest>(bytes, Json) ?? throw new InvalidDataException("更新清单为空。");
        if (m.KeyId is null || !trust.Keys.TryGetValue(m.KeyId, out var pem)) throw new InvalidDataException("无法识别发行签名，请从官方 Releases 手动安装接入版。");
        using var rsa = RSA.Create(); rsa.ImportFromPem(pem);
        if (!rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) throw new InvalidDataException("更新清单签名无效。");
        ParseVersion(m.Version); ParseVersion(m.MinUpdaterVersion);
        if (m.SchemaVersion != 1 || m.Channel != "stable" || m.Rid != "win-x64" || m.MinWindowsBuild < 0)
            throw new InvalidDataException("不支持的更新清单格式或平台。");
        if (m.Size <= 0 || m.Size > MaxPackageSize || m.Sha256 is null || !Regex.IsMatch(m.Sha256, @"\A[0-9a-fA-F]{64}\z"))
            throw new InvalidDataException("安装包大小或校验值无效。");
        if (m.AssetName != $"PTBox-Setup-{m.Version}-win-x64.exe" || m.DownloadUrl != $"https://github.com/{trust.Repository}/releases/download/v{m.Version}/{m.AssetName}")
            throw new InvalidDataException("安装包不是指定仓库的发行附件。");
        return m;
    }

    public static void CheckCompatibility(UpdateManifest m, string current, int windowsBuild)
    {
        if (ParseVersion(m.Version) <= ParseVersion(current)) throw new InvalidDataException("只允许升级到更高版本。");
        if (m.MinWindowsBuild > windowsBuild || ParseVersion(m.MinUpdaterVersion) > ParseVersion(UpdaterVersion))
            throw new InvalidDataException("此更新需要更高系统或更新器版本，请查看 Releases 手动安装说明。");
    }

    // Hold the returned read handle until setup exits: the verified package cannot be replaced or written meanwhile.
    public static async Task<FileStream> OpenVerifiedPackageAsync(string path, UpdateManifest m, CancellationToken token = default)
    {
        UpdateJobs.NoReparsePoints(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("安装包不能是链接文件。");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        try
        {
            if (stream.Length != m.Size) throw new InvalidDataException("安装包大小不匹配。");
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
            if (!hash.Equals(m.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("安装包 SHA256 校验失败。");
            stream.Position = 0; return stream;
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    public static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(value, Json)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
