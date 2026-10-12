using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AppBridge;

/// <summary>Backup integrity, path validation and basic device compatibility checks.</summary>
static class Storage
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string Hash(string path)
    {
        using var f = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(f));
    }
    public static bool PackageValid(string s) => Regex.IsMatch(s, @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+$");
    /// <summary>Resolves a manifest path within the backup directory and rejects links and traversal.</summary>
    public static string Resolve(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new Exception("Недопустимый путь в резервной копии.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Путь выходит за папку резервной копии.");
        var current = new FileInfo(full).Directory;
        if (File.Exists(full) && File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            throw new Exception("Ссылки в копии не поддерживаются.");
        while (current != null && current.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint))
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
    public static Backup Load(string folder)
    {
        var b = JsonSerializer.Deserialize<Backup>(File.ReadAllText(Path.Combine(folder, "backup.json"))) ?? throw new Exception("Пустое описание копии.");
        if (!PackageValid(b.Package) || b.MinSdk < 1 || b.Files.Length == 0)
            throw new Exception("Некорректное описание копии.");
        if (b.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != b.Files.Length)
            throw new Exception("Повторяющиеся пути в копии.");
        foreach (var f in b.Files)
        {
            var p = Resolve(folder, f.Path);
            if (!(f.Path.StartsWith("apk/", StringComparison.Ordinal) && f.Path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) && !f.Path.StartsWith("obb/", StringComparison.Ordinal))
                throw new Exception("Неизвестный тип файла в копии.");
            if (!File.Exists(p) || new FileInfo(p).Length != f.Size || !Hash(p).Equals(f.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Файл отсутствует или повреждён: {f.Path}");
        }
        if (!b.Files.Any(f => f.Path == "apk/base.apk"))
            throw new Exception("В копии нет base.apk.");
        return b;
    }
    /// <summary>Checks minimum API and ABI only; successful installation and runtime behavior need device testing.</summary>
    public static string? Compatibility(Backup b, int sdk, string[] abis)
    {
        if (sdk < b.MinSdk)
            return $"Требуется Android API {b.MinSdk}, на устройстве API {sdk}.";
        if (b.Abis.Length > 0 && !b.Abis.Intersect(abis).Any())
            return $"Архитектуры несовместимы: приложение {string.Join(", ", b.Abis)}, устройство {string.Join(", ", abis)}.";
        return null;
    }
}
