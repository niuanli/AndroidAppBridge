namespace AndroidAppBridge.Tests.Fixtures;

sealed class FakeDeviceGateway : IDeviceGateway
{
    public List<string> Calls { get; } = [];
    public InstalledPackage Before = new(new("org.example.game", "1", 5, 21, 35, ""), ["/data/app/fixture/base.apk"]);
    public InstalledPackage? After;
    public DeviceInfo Info = new(36, ["arm64-v8a"], 4096, 10000);
    public string[] Obbs = [];
    public Exception? ObbQueryError, PushError, InstallError;
    public bool Installed;
    int reads;
    public Task<Device[]> GetDevicesAsync(CancellationToken ct) => Task.FromResult(Array.Empty<Device>());
    public Task<string[]> GetAppsAsync(string serial, CancellationToken ct) => Task.FromResult(new[] { "org.example.game" });
    public Task<InstalledPackage> GetPackageAsync(string serial, string package, CancellationToken ct) => Task.FromResult(reads++ == 0 ? Before : After ?? Before);
    public Task<string[]> GetObbPathsAsync(string serial, string package, CancellationToken ct) => ObbQueryError == null ? Task.FromResult(Obbs) : Task.FromException<string[]>(ObbQueryError);
    public Task<string> PullAsync(string serial, string remote, string local, IProgress<FileTransfer>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add("pull:" + remote);
        return Task.FromResult("pulled");
    }
    public Task<DeviceInfo> GetDeviceInfoAsync(string serial, CancellationToken ct) => Task.FromResult(Info);
    public Task<bool> IsInstalledAsync(string serial, string package, CancellationToken ct) => Task.FromResult(Installed);
    public Task<string> InstallAsync(string serial, string[] apks, bool replace, CancellationToken ct)
    {
        Calls.Add("install:" + replace);
        return InstallError == null ? Task.FromResult("Success") : Task.FromException<string>(InstallError);
    }
    public Task<string> PushObbAsync(string serial, string local, string remote, CancellationToken ct)
    {
        Calls.Add("push:" + remote);
        return PushError == null ? Task.FromResult("pushed") : Task.FromException<string>(PushError);
    }
    public Task<string> LaunchAsync(string serial, string package, CancellationToken ct)
    {
        Calls.Add("launch:" + package);
        return Task.FromResult("Status: ok");
    }
}

sealed class FakeBackupRepository : IBackupRepository
{
    public Backup Manifest = new("org.example.game", "1", 21, 35, ["arm64-v8a"], [new("apk/base.apk", new string('A', 64), 100), new("obb/main.4.org.example.game.obb", new string('B', 64), 100)], "fixture");
    public Backup? ChangedManifest;
    public int Seals, Loads;
    public string NewFolder(string package, string source) => "fixture-backup";
    public string Resolve(string folder, string relative) => folder + "/" + relative;
    public string CreateDestination(string folder, string relative) => Resolve(folder, relative);
    public Task<Backup> LoadAsync(string folder, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Loads++ == 0 ? Manifest : ChangedManifest ?? Manifest);
    }
    public Task<Backup> SealAsync(string folder, ApkInfo info, CancellationToken ct)
    {
        Seals++;
        return Task.FromResult(Manifest);
    }
}

sealed class FakeImporter : ILocalPackageImporter
{
    public int Saves;
    public LocalPackage Inspect(string[] apks, string[] obbs) => new(new("org.example.game", "1", 5, 21, 35, ""), [], []);
    public string[] FindObbs(string apk, string package) => [];
    public Task<Backup> SaveAsync(LocalPackage package, string folder, IProgress<FileTransfer>? progress, CancellationToken ct)
    {
        Saves++;
        return Task.FromResult(new FakeBackupRepository().Manifest);
    }
}
