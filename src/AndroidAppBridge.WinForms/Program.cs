using AndroidAppBridge.Infrastructure;
using AndroidAppBridge.Presentation;

namespace AndroidAppBridge;

/// <summary>Composition root: concrete adapters are connected here, never inside views or use cases.</summary>
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var folder = AppContext.BaseDirectory;
        var importer = new LocalPackageImporter();
        var repository = new FileBackupRepository(Path.Combine(folder, "Backups"));
        var client = new AdbClient(Path.Combine(folder, "platform-tools", "adb.exe"), new ProcessRunner());
        var service = new TransferService(new AdbDeviceGateway(client), repository, importer);
        BridgeForm Factory(bool preview) => new(service, importer, new FileUserPreferences(folder), preview);
        if (UiDiagnostics.Run(args, Factory, importer))
            return;
        System.Windows.Forms.Application.Run(Factory(false));
    }
}
