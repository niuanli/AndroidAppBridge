namespace AndroidAppBridge.Infrastructure;

public sealed class LocalPackageImporter : ILocalPackageImporter
{
    public LocalPackage Inspect(string[] apks, string[] obbs) => PackageImport.Inspect(apks, obbs);
    public string[] FindObbs(string apk, string package) => PackageImport.FindObbs(apk, package);
    public Task<Backup> SaveAsync(LocalPackage package, string folder, IProgress<FileTransfer>? progress, CancellationToken ct) => PackageImport.Save(package, folder,
        (name, done, total) => progress?.Report(new(name, done, total)), ct);
}
