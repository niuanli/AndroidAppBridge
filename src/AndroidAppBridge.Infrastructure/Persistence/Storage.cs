using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AndroidAppBridge.Infrastructure;

/// <summary>Backup integrity, path validation and basic device compatibility checks.</summary>
public static class Storage
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string Hash(string path, CancellationToken ct = default)
    {
        using var f = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        int length;
        while ((length = f.Read(buffer)) != 0)
        {
            ct.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, length);
        }
        ct.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static bool PackageValid(string s) => PackageRules.IsValidName(s);
    /// <summary>Resolves a manifest path within the backup directory and rejects links and traversal.</summary>
    public static string Resolve(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new Exception("Недопустимый путь в резервной копии.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Directory.Exists(root) && File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Ссылки в копии не поддерживаются.");
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Путь выходит за папку резервной копии.");
        var current = new FileInfo(full).Directory;
        if (File.Exists(full) && File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            throw new Exception("Ссылки в копии не поддерживаются.");
        while (current != null && current.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new Exception("Ссылки в копии не поддерживаются.");
            current = current.Parent;
        }
        return full;
    }
    /// <summary>Reads native library architectures from APK ZIP entries without executing their contents.</summary>
    public static string[] Abis(IEnumerable<string> apks) => apks.SelectMany(p =>
    {
        using var zip = ZipFile.OpenRead(p);
        return zip.Entries.Select(e => e.FullName.Split('/')).Where(s => s.Length >= 3 && s[0] == "lib" && s[^1].EndsWith(".so")).Select(s => s[1]).ToArray();
    }).Distinct().Order().ToArray();
    /// <summary>Validates every manifest entry, file size and digest before a backup can be installed.</summary>
    public static Backup Load(string folder, CancellationToken ct = default)
    {
        var b = JsonSerializer.Deserialize<Backup>(File.ReadAllText(Path.Combine(folder, "backup.json"))) ?? throw new Exception("Пустое описание копии.");
        if (!PackageValid(b.Package) || b.MinSdk < 1 || b.TargetSdk < 0 || b.Files == null || b.Abis == null || b.Files.Length == 0 || b.Files.Any(f => f == null || string.IsNullOrEmpty(f.Path) || f.Size < 0 || f.Sha256 == null || !Regex.IsMatch(f.Sha256, "^[A-Fa-f0-9]{64}$")))
            throw new Exception("Некорректное описание копии.");
        if (b.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != b.Files.Length)
            throw new Exception("Повторяющиеся пути в копии.");
        foreach (var f in b.Files)
        {
            ct.ThrowIfCancellationRequested();
            var p = Resolve(folder, f.Path);
            if (!(f.Path.StartsWith("apk/", StringComparison.Ordinal) && f.Path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) && !f.Path.StartsWith("obb/", StringComparison.Ordinal))
                throw new Exception("Неизвестный тип файла в копии.");
            if (!File.Exists(p) || new FileInfo(p).Length != f.Size || !Hash(p, ct).Equals(f.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Файл отсутствует или повреждён: {f.Path}");
        }
        var apks = b.Files.Where(f => f.Path.StartsWith("apk/", StringComparison.Ordinal))
            .Select(f => ApkManifest.Read(Resolve(folder, f.Path))).ToArray();
        var captured = PackageSelectionPolicy.Validate(apks);
        if (captured.Package != b.Package)
            throw new InvalidDataException("Некорректное описание копии.");
        return b;
    }
    /// <summary>Commits a complete manifest atomically; interruption leaves no installable partial JSON.</summary>
    public static void WriteManifest(string folder, Backup backup, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var destination = Resolve(folder, "backup.json");
        var temporary = Resolve(folder, "backup.json.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(backup, JsonOptions));
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, destination, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
