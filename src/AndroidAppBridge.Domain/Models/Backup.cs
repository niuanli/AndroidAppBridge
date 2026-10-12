namespace AndroidAppBridge.Domain;

/// <summary>ADB device identity and connection state used by both device selectors.</summary>
public record Device(string Serial, string State, string Model)
{
    public override string ToString() => $"{Model} · {Serial} · {State}";
}
/// <summary>A backup-relative path, SHA-256 digest and byte count recorded after copying.</summary>
public record SavedFile(string Path, string Sha256, long Size);
/// <summary>Portable backup manifest stored as backup.json alongside APK and OBB files.</summary>
public record Backup(string Package, string Version, int MinSdk, int TargetSdk, string[] Abis, SavedFile[] Files, string Created);
