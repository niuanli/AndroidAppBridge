namespace AndroidAppBridge.Tests.Domain;

[TestFixture]
public class CompatibilityTests
{
    static Backup Backup(string[]? abis = null, int min = 21, long size = 100) => new("org.example.game", "1", min, 35, abis ?? ["arm64-v8a"], [new("apk/base.apk", new string('A', 64), size)], "fixture");
    [Test] public void SupportsMatchingArchitecture() => Assert.That(CompatibilityPolicy.Check(Backup(), new(36, ["arm64-v8a"], 4096, 1000)), Is.Null);
    [Test] public void RejectsInsufficientApi() => Assert.That(CompatibilityPolicy.Check(Backup(min: 30), new(29, ["arm64-v8a"], 4096, null)), Does.Contain("API 30"));
    [Test] public void RejectsAbiMismatch() => Assert.That(CompatibilityPolicy.Check(Backup(), new(36, ["armeabi-v7a"], 4096, null)), Does.Contain("Архитектуры"));
    [Test] public void JavaOnlyPackageDoesNotNeedAnAbiMatch() => Assert.That(CompatibilityPolicy.Check(Backup([]), new(36, ["x86"], 4096, null)), Is.Null);
    [TestCase(199, false)]
    [TestCase(200, true)]
    [TestCase(201, true)]
    public void RequiresTwoTimesTheBackupSize(long space, bool accepted) => Assert.That(CompatibilityPolicy.Check(Backup(), new(36, ["arm64-v8a"], 4096, space)) == null, Is.EqualTo(accepted));
    [Test] public void DoesNotOverflowWhenCheckingVeryLargeFiles() => Assert.That(CompatibilityPolicy.Check(Backup(size: long.MaxValue / 2 + 1), new(36, ["arm64-v8a"], 4096, long.MaxValue)), Is.Not.Null);
    [Test]
    public void FingerprintIgnoresFileOrderButRejectsChangedContent()
    {
        var b = Backup() with
        {
            Files = [new("apk/base.apk", "A", 1), new("obb/main.obb", "B", 2)]
        };
        Assert.That(CompatibilityPolicy.SameFiles(b, b with
        {
            Files = b.Files.Reverse().ToArray()
        }), Is.True);
        Assert.That(CompatibilityPolicy.SameFiles(b, b with
        {
            Files = [b.Files[0], b.Files[1] with { Sha256 = "C" }]
        }), Is.False);
    }
}
