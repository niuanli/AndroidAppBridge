namespace AndroidAppBridge.Application;

public interface ILocalPackageImporter
{
    LocalPackage Inspect(string[] apks, string[] obbs);
    string[] FindObbs(string apk, string package);
    Task<Backup> SaveAsync(LocalPackage package, string folder, IProgress<FileTransfer>? progress, CancellationToken ct);
}

public interface IUserPreferences
{
    bool English
    {
        get; set;
    }
}
