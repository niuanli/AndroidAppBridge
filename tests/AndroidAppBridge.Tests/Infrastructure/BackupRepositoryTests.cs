using System.Text.Json;

namespace AndroidAppBridge.Tests.Infrastructure;

[TestFixture]
public class BackupRepositoryTests
{
    [TestCase("../outside.apk")]
    [TestCase("apk/../../outside.apk")]
    [TestCase("C:\\outside.apk")]
    [TestCase("/outside.apk")]
    public void RejectsPathsOutsideTheBackup(string path)
    {
        using var temp = new TempDirectory();
        Assert.That(() => Storage.Resolve(temp.Root, path), Throws.Exception);
    }
    [Test]
    public void ResolvesANewSubfolderWithoutTreatingItAsALink()
    {
        using var temp = new TempDirectory();
        Assert.That(Storage.Resolve(temp.Root, "apk/base.apk"), Is.EqualTo(temp.FilePath("apk/base.apk")));
    }
    [Test]
    public async Task RoundtripDetectsFileTampering()
    {
        using var temp = new TempDirectory();
        var repository = new FileBackupRepository(temp.Root);
        var folder = repository.NewFolder("org.example.game", "device");
        var apk = temp.Apk();
        File.Copy(apk, repository.CreateDestination(folder, "apk/base.apk"));
        await repository.SealAsync(folder, ApkManifest.Read(apk), CancellationToken.None);
        Assert.That((await repository.LoadAsync(folder, CancellationToken.None)).Package, Is.EqualTo("org.example.game"));
        File.AppendAllText(Path.Combine(folder, "apk/base.apk"), "tamper");
        Assert.ThrowsAsync<Exception>(() => repository.LoadAsync(folder, CancellationToken.None));
    }
    [Test]
    public void RejectsMissingArraysInManifest()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.FilePath("backup.json"), "{\"Package\":\"org.example.game\",\"MinSdk\":21}");
        Assert.That(() => Storage.Load(temp.Root), Throws.Exception);
    }
    [Test]
    public void RejectsDuplicateManifestPaths()
    {
        using var temp = new TempDirectory();
        var file = new SavedFile("apk/base.apk", new string('A', 64), 1);
        var b = new Backup("org.example.game", "1", 21, 35, [], [file, file], "fixture");
        File.WriteAllText(temp.FilePath("backup.json"), JsonSerializer.Serialize(b));
        Assert.That(() => Storage.Load(temp.Root), Throws.Exception);
    }
    [Test]
    public void CancellationDoesNotWriteManifestOrTemporaryFile()
    {
        using var temp = new TempDirectory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var b = new FakeBackupRepository().Manifest;
        Assert.Throws<OperationCanceledException>(() => Storage.WriteManifest(temp.Root, b, cts.Token));
        Assert.That(Directory.GetFiles(temp.Root), Is.Empty);
    }
    [Test]
    public void DoesNotReplaceAnAlreadyCommittedManifest()
    {
        using var temp = new TempDirectory();
        var b = new FakeBackupRepository().Manifest;
        Storage.WriteManifest(temp.Root, b, CancellationToken.None);
        var original = File.ReadAllText(temp.FilePath("backup.json"));
        Assert.Throws<IOException>(() => Storage.WriteManifest(temp.Root, b with { Version = "2" }, CancellationToken.None));
        Assert.That(File.ReadAllText(temp.FilePath("backup.json")), Is.EqualTo(original));
        Assert.That(File.Exists(temp.FilePath("backup.json.tmp")), Is.False);
    }
    [Test]
    public void CancellationStopsHashing()
    {
        using var temp = new TempDirectory();
        var file = temp.Obb();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => Storage.Hash(file, cts.Token));
    }
}
