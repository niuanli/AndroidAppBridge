namespace AndroidAppBridge.Domain;

public record ImportSource(string Path, string Destination);
public record LocalPackage(ApkInfo Info, ImportSource[] Files, string[] Abis);
