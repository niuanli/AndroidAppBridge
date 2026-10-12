namespace AndroidAppBridge.Infrastructure;

/// <summary>Repository for validated backup manifests and their owned files.</summary>
public sealed class FileBackupRepository(string root) : IBackupRepository
{
    public string NewFolder(string package, string source)
    {
        if (!PackageRules.IsValidName(package) || source is not ("device" or "import"))
            throw new ArgumentException("Некорректное имя пакета APK.");
        return Path.Combine(root, package + "_" + source + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N"));
    }
    public string Resolve(string folder, string relative) => Storage.Resolve(folder, relative);
    public string CreateDestination(string folder, string relative)
    {
        var path = Resolve(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }
    public Task<Backup> LoadAsync(string folder, CancellationToken ct) => Task.Run(() => Storage.Load(folder, ct), ct);
    public Task<Backup> SealAsync(string folder, ApkInfo info, CancellationToken ct) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        var apks = Directory.GetFiles(Path.Combine(folder, "apk"), "*.apk");
        if (!apks.Any(p => Path.GetFileName(p) == "base.apk") || info.MinSdk < 1)
            throw new InvalidDataException("Нет base.apk в извлечённом пакете.");
        var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(p =>
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(folder, p).Replace('\\', '/');
            Storage.Resolve(folder, relative);
            return new SavedFile(relative, Storage.Hash(p, ct), new FileInfo(p).Length);
        }).ToArray();
        var backup = new Backup(info.Package, info.Version, info.MinSdk, info.TargetSdk, Storage.Abis(apks), files, DateTimeOffset.Now.ToString("O"));
        Storage.WriteManifest(folder, backup, ct);
        return backup;
    }, ct);
}
