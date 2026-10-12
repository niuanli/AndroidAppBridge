using System.Text.RegularExpressions;

namespace AndroidAppBridge.Domain;

/// <summary>Rules for identifiers crossing the backup and Android shell boundaries.</summary>
public static class PackageRules
{
    public static bool IsValidName(string? name) => name != null && Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+$");
    public static bool IsSafeRemoteApk(string path) => Regex.IsMatch(path, @"^/[A-Za-z0-9_./~+=-]+\.apk$") && !path.Split('/').Contains("..");
    public static bool IsSafeObbRelative(string path) => path.Length > 0 && !path.StartsWith('/') && Regex.IsMatch(path, @"^[A-Za-z0-9_./~+=-]+$") && !path.Split('/').Any(s => s is ".." or "." or "");
}
