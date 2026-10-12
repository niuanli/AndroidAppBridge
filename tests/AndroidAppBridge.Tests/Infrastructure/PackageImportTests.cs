namespace AndroidAppBridge.Tests.Infrastructure;

[TestFixture]
public class PackageImportTests
{
    [Test]
    public void RequiresExactlyOneBaseApk()
    {
        using var temp = new TempDirectory();
        var apk = temp.Apk();
        var second = temp.Apk("second.apk");
        var split = temp.Apk("split.apk", split: "config.arm64_v8a");
        Assert.Throws<InvalidDataException>(() => PackageImport.Inspect([split], []));
        Assert.Throws<InvalidDataException>(() => PackageImport.Inspect([apk, second], []));
    }
    [Test]
    public void RejectsMixedPackages()
    {
        using var temp = new TempDirectory();
        var apk = temp.Apk();
        var split = temp.Apk("split.apk", "org.example.other", "config.en");
        Assert.Throws<InvalidDataException>(() => PackageImport.Inspect([apk, split], []));
    }
    [Test]
    public void RejectsWrongObbPackage()
    {
        using var temp = new TempDirectory();
        Assert.Throws<InvalidDataException>(() => PackageImport.Inspect([temp.Apk()], [temp.Obb("main.4.org.example.other.obb")]));
    }
    [Test]
    public void FindsObbInPackageSubfolderAndReportsMissingObb()
    {
        using var temp = new TempDirectory();
        var apk = temp.Apk();
        Directory.CreateDirectory(temp.FilePath("org.example.game"));
        var obb = temp.Obb("org.example.game/main.4.org.example.game.obb");
        Assert.That(PackageImport.FindObbs(apk, "org.example.game"), Is.EqualTo(new[] { obb }));
        Assert.That(PackageImport.FindObbs(apk, "org.example.missing"), Is.Empty);
    }
    [Test]
    public async Task ImportsSplitsAndReusedExpansionVersionWithoutChangingOriginalFiles()
    {
        using var temp = new TempDirectory();
        var apk = temp.Apk();
        var split = temp.Apk("split.apk", split: "config.arm64_v8a");
        var obb = temp.Obb();
        var before = Storage.Hash(apk);
        var selected = PackageImport.Inspect([apk, split], [obb]);
        var folder = temp.FilePath("backup");
        await PackageImport.Save(selected, folder, (_, _, _) => { }, CancellationToken.None);
        Assert.Multiple(() => { Assert.That(Storage.Load(folder).Files.Length, Is.EqualTo(3)); Assert.That(Storage.Hash(apk), Is.EqualTo(before)); Assert.That(File.Exists(obb), Is.True); });
    }
    [Test]
    public void RejectsDuplicateSplitSelection()
    {
        using var temp = new TempDirectory();
        var apk = temp.Apk();
        var split = temp.Apk("split.apk", split: "config.en");
        Assert.Throws<InvalidDataException>(() => PackageImport.Inspect([apk, split, split], []));
    }
    [Test]
    public void CancellationDoesNotCommitAManifest()
    {
        using var temp = new TempDirectory();
        var package = PackageImport.Inspect([temp.Apk()], []);
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        var folder = temp.FilePath("canceled");
        Assert.ThrowsAsync<OperationCanceledException>(() => PackageImport.Save(package, folder, (_, _, _) => { }, ct.Token));
        Assert.That(File.Exists(Path.Combine(folder, "backup.json")), Is.False);
    }
}
