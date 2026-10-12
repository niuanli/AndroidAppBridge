namespace AndroidAppBridge.Domain;

/// <summary>Preflight policy; Android remains authoritative for signature, target SDK and runtime checks.</summary>
public static class CompatibilityPolicy
{
    public static string? Check(Backup backup, DeviceInfo device)
    {
        if (device.Sdk < backup.MinSdk)
            return $"Требуется Android API {backup.MinSdk}, на устройстве API {device.Sdk}.";
        if (backup.Abis.Length > 0 && !backup.Abis.Intersect(device.Abis, StringComparer.Ordinal).Any())
            return $"Архитектуры несовместимы: приложение {string.Join(", ", backup.Abis)}, устройство {string.Join(", ", device.Abis)}.";
        var size = backup.Files.Aggregate(0L, (total, f) => checked(total + f.Size));
        if (device.FreeBytes is long free && (free < size || free - size < size))
            return "Недостаточно свободного места с запасом для установки.";
        return null;
    }

    public static bool SameFiles(Backup first, Backup second) => first.Package == second.Package && first.Version == second.Version
        && first.MinSdk == second.MinSdk && first.TargetSdk == second.TargetSdk && first.Abis.SequenceEqual(second.Abis)
        && first.Files.OrderBy(f => f.Path, StringComparer.Ordinal).SequenceEqual(second.Files.OrderBy(f => f.Path, StringComparer.Ordinal));
}
