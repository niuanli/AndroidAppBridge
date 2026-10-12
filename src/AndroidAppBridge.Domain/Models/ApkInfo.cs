namespace AndroidAppBridge.Domain;

public record ApkInfo(string Package, string Version, long VersionCode, int MinSdk, int TargetSdk, string Split);
