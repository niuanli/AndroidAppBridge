namespace AndroidAppBridge.Tests.Application;

[TestFixture]
public class TransferServiceTests
{
    FakeDeviceGateway device = null!; FakeBackupRepository backups = null!; FakeImporter importer = null!; TransferService service = null!;
    [SetUp]
    public void Setup()
    {
        device = new();
        backups = new();
        importer = new();
        service = new(device, backups, importer);
    }
    [Test]
    public async Task ImportDoesNotNeedAConnectedSourceDevice()
    {
        var result = await service.ImportAsync(["game.apk"], [], null, CancellationToken.None);
        Assert.Multiple(() => { Assert.That(result.Manifest.Package, Is.EqualTo("org.example.game")); Assert.That(importer.Saves, Is.EqualTo(1)); Assert.That(device.Calls, Is.Empty); });
    }
    [Test]
    public void CanceledImportDoesNotSnapshotFiles()
    {
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(() => service.ImportAsync(["game.apk"], [], null, ct.Token));
        Assert.That(importer.Saves, Is.Zero);
    }
    [Test]
    public async Task ExportCommitsOnlyAfterCopyAndVersionRecheck()
    {
        await service.ExportAsync("phone", "org.example.game", false, null, CancellationToken.None);
        Assert.That(device.Calls, Is.EqualTo(new[] { "pull:/data/app/fixture/base.apk" }));
        Assert.That(backups.Seals, Is.EqualTo(1));
    }
    [Test]
    public void ExportRejectsAnUpdateDuringCopy()
    {
        device.After = device.Before with
        {
            Info = device.Before.Info with
            {
                VersionCode = 6
            }
        };
        Assert.ThrowsAsync<IOException>(() => service.ExportAsync("phone", "org.example.game", false, null, CancellationToken.None));
        Assert.That(backups.Seals, Is.Zero);
    }
    [Test]
    public void ExportRejectsUnsafeRemoteApkBeforePull()
    {
        device.Before = device.Before with
        {
            ApkPaths = ["/data/app/pkg/../base.apk"]
        };
        Assert.ThrowsAsync<InvalidDataException>(() => service.ExportAsync("phone", "org.example.game", false, null, CancellationToken.None));
        Assert.That(device.Calls, Is.Empty);
    }
    [Test]
    public void ExportRejectsAnObbOutsideThePackageFolder()
    {
        device.Obbs = ["/sdcard/Android/obb/other/main.obb"];
        Assert.ThrowsAsync<InvalidDataException>(() => service.ExportAsync("phone", "org.example.game", true, null, CancellationToken.None));
        Assert.That(backups.Seals, Is.Zero);
    }
    [Test]
    public async Task MissingOptionalObbDoesNotDiscardTheApkBackup()
    {
        device.ObbQueryError = new IOException("missing");
        await service.ExportAsync("phone", "org.example.game", true, null, CancellationToken.None);
        Assert.That(backups.Seals, Is.EqualTo(1));
    }
    [Test]
    public void CancellationOfObbQueryDoesNotSealTheBackup()
    {
        device.ObbQueryError = new OperationCanceledException();
        Assert.ThrowsAsync<OperationCanceledException>(() => service.ExportAsync("phone", "org.example.game", true, null, CancellationToken.None));
        Assert.That(backups.Seals, Is.Zero);
    }
    [TestCase(true)]
    [TestCase(false)]
    public async Task PreparationReportsWhetherInstallationWillReplaceAnExistingApp(bool installed)
    {
        device.Installed = installed;
        var plan = await service.PrepareInstallAsync("tablet", "backup", null, CancellationToken.None);
        Assert.That(plan.ReplaceExisting, Is.EqualTo(installed));
        Assert.That(device.Calls, Is.Empty);
    }
    [Test]
    public void PreparationRejectsIncompatibleDeviceWithoutInstalling()
    {
        device.Info = device.Info with
        {
            Abis = ["armeabi-v7a"]
        };
        Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareInstallAsync("tablet", "backup", null, CancellationToken.None));
        Assert.That(device.Calls, Is.Empty);
    }
    [Test]
    public async Task InstallRechecksTheApprovedCopyAndPushesObbAfterApk()
    {
        var plan = await service.PrepareInstallAsync("tablet", "backup", null, CancellationToken.None);
        await service.InstallAsync(plan, null, CancellationToken.None);
        Assert.That(backups.Loads, Is.EqualTo(2));
        Assert.That(device.Calls, Is.EqualTo(new[] { "install:False", "push:/sdcard/Android/obb/org.example.game/main.4.org.example.game.obb" }));
    }
    [Test]
    public async Task ChangedCopyAfterConfirmationIsRejectedWithoutInstalling()
    {
        var plan = await service.PrepareInstallAsync("tablet", "backup", null, CancellationToken.None);
        backups.ChangedManifest = backups.Manifest with
        {
            Version = "2"
        };
        Assert.ThrowsAsync<IOException>(() => service.InstallAsync(plan, null, CancellationToken.None));
        Assert.That(device.Calls, Is.Empty);
    }
    [Test]
    public async Task FailedInstallNeverPushesObb()
    {
        device.InstallError = new IOException("INSTALL_FAILED_USER_RESTRICTED");
        var plan = await service.PrepareInstallAsync("tablet", "backup", null, CancellationToken.None);
        Assert.ThrowsAsync<IOException>(() => service.InstallAsync(plan, null, CancellationToken.None));
        Assert.That(device.Calls, Has.Count.EqualTo(1));
    }
    [Test]
    public async Task FailedObbIsReportedAsPartialSuccessRatherThanUndoingApk()
    {
        device.PushError = new IOException("denied");
        var plan = await service.PrepareInstallAsync("tablet", "backup", null, CancellationToken.None);
        var error = Assert.ThrowsAsync<PartialTransferException>(() => service.InstallAsync(plan, null, CancellationToken.None));
        Assert.That(error!.InnerException, Is.SameAs(device.PushError));
        Assert.That(device.Calls[0], Is.EqualTo("install:False"));
    }
    [Test]
    public async Task CancelingObbKeepsCancellationSemanticsAndLogsInstalledApk()
    {
        device.PushError = new OperationCanceledException();
        var plan = await service.PrepareInstallAsync("tablet", "backup", null, CancellationToken.None);
        var events = new List<TransferUpdate>();
        Assert.ThrowsAsync<OperationCanceledException>(() => service.InstallAsync(plan, new DelegateProgress<TransferUpdate>(events.Add), CancellationToken.None));
        Assert.That(events.Any(e => e.Text.Contains("APK уже установлены")), Is.True);
    }
    [Test]
    public async Task LaunchUsesTheChosenDeviceAndPackage()
    {
        await service.LaunchAsync("tablet", "org.example.game", null, CancellationToken.None);
        Assert.That(device.Calls, Is.EqualTo(new[] { "launch:org.example.game" }));
    }
}
