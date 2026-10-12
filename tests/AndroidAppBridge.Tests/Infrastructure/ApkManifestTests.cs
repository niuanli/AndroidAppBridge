namespace AndroidAppBridge.Tests.Infrastructure;

[TestFixture]
public class ApkManifestTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void ReadsBinaryManifestInBothStringEncodings(bool utf8)
    {
        var info = ApkManifest.Parse(ManifestFixture.Manifest(utf8));
        Assert.Multiple(() => { Assert.That(info.Package, Is.EqualTo("org.example.game")); Assert.That(info.Version, Is.EqualTo("1.0")); Assert.That(info.VersionCode, Is.EqualTo(5)); Assert.That(info.MinSdk, Is.EqualTo(21)); Assert.That(info.TargetSdk, Is.EqualTo(35)); });
    }
    [Test] public void ReadsTheSplitIdentity() => Assert.That(ApkManifest.Parse(ManifestFixture.Manifest(true, split: "config.arm64_v8a")).Split, Is.EqualTo("config.arm64_v8a"));
    [Test] public void RejectsTruncatedData() => Assert.Throws<InvalidDataException>(() => ApkManifest.Parse([3, 0, 8, 0]));
    [Test]
    public void RejectsZeroLengthChunks()
    {
        var bytes = ManifestFixture.Manifest(true);
        Array.Clear(bytes, 12, 4);
        Assert.Throws<InvalidDataException>(() => ApkManifest.Parse(bytes));
    }
    [Test] public void RejectsInvalidPackageName() => Assert.Throws<InvalidDataException>(() => ApkManifest.Parse(ManifestFixture.Manifest(true, "org.example;reboot")));
    [Test]
    public void ReadsMetadataAndArchitectureWithoutExecutingApk()
    {
        using var temp = new TempDirectory();
        var apk = temp.Apk(abi: "arm64-v8a");
        Assert.That(ApkManifest.Read(apk).Package, Is.EqualTo("org.example.game"));
        Assert.That(Storage.Abis([apk]), Is.EqualTo(new[] { "arm64-v8a" }));
    }
}
