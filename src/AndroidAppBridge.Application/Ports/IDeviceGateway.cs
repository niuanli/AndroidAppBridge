namespace AndroidAppBridge.Application;

/// <summary>Android operations needed by use cases, independent of the ADB process implementation.</summary>
public interface IDeviceGateway
{
    Task<Device[]> GetDevicesAsync(CancellationToken ct);
    Task<string[]> GetAppsAsync(string serial, CancellationToken ct);
    Task<InstalledPackage> GetPackageAsync(string serial, string package, CancellationToken ct);
    Task<string[]> GetObbPathsAsync(string serial, string package, CancellationToken ct);
    Task<string> PullAsync(string serial, string remote, string local, IProgress<FileTransfer>? progress, CancellationToken ct);
    Task<DeviceInfo> GetDeviceInfoAsync(string serial, CancellationToken ct);
    Task<bool> IsInstalledAsync(string serial, string package, CancellationToken ct);
    Task<string> InstallAsync(string serial, string[] apks, bool replace, CancellationToken ct);
    Task<string> PushObbAsync(string serial, string local, string remote, CancellationToken ct);
    Task<string> LaunchAsync(string serial, string package, CancellationToken ct);
}
