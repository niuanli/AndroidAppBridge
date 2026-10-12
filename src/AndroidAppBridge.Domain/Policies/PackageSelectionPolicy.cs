using System.Text.RegularExpressions;

namespace AndroidAppBridge.Domain;

/// <summary>Information expert for the identities shared by a base APK, splits and expansion files.</summary>
public static class PackageSelectionPolicy
{
    public static ApkInfo Validate(ApkInfo[] apks)
    {
        if (apks.Length == 0)
            throw new InvalidDataException("Выберите хотя бы один APK.");
        var bases = apks.Where(i => i.Split.Length == 0).ToArray();
        if (bases.Length != 1)
            throw new InvalidDataException("Выберите один основной APK и, при необходимости, его split APK.");
        var info = bases[0];
        if (apks.Any(i => i.Package != info.Package || i.VersionCode != info.VersionCode))
            throw new InvalidDataException("Все APK должны иметь одинаковые пакет и код версии.");
        if (apks.Select(i => i.Split).Distinct(StringComparer.OrdinalIgnoreCase).Count() != apks.Length)
            throw new InvalidDataException("APK или OBB выбран повторно.");
        return info;
    }
    public static bool MatchesObb(string filename, string package) => PackageRules.IsValidName(package)
        && Regex.IsMatch(filename, @"^(main|patch)\.\d+\." + Regex.Escape(package) + @"\.obb$");
}
