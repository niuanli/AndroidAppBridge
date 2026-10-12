namespace AndroidAppBridge.Application;

public interface IBackupRepository
{
    string NewFolder(string package, string source);
    string Resolve(string folder, string relative);
    string CreateDestination(string folder, string relative);
    Task<Backup> LoadAsync(string folder, CancellationToken ct);
    Task<Backup> SealAsync(string folder, ApkInfo info, CancellationToken ct);
}
