namespace AndroidAppBridge.Tests.Domain;

[TestFixture]
public class PackageSelectionTests
{
    static ApkInfo Base => new("org.example.game", "1", 5, 21, 35, "");
    [Test] public void RejectsDifferentVersionCode() => Assert.Throws<InvalidDataException>(() => PackageSelectionPolicy.Validate([Base, Base with { Split = "config.en", VersionCode = 6 }]));
    [Test] public void AcceptsSplitMetadataWithSameIdentity() => Assert.That(PackageSelectionPolicy.Validate([Base, Base with { Split = "config.en" }]), Is.EqualTo(Base));
    [TestCase("main.4.org.example.game.obb", true)]
    [TestCase("patch.5.org.example.game.obb", true)]
    [TestCase("main.5.org.example.game.other.obb", false)]
    [TestCase("main.5.org.example.game.obb;reboot", false)]
    [TestCase("org.example.game.obb", false)]
    public void ExpansionNameMustIdentifyTheExactPackage(string file, bool valid) => Assert.That(PackageSelectionPolicy.MatchesObb(file, "org.example.game"), Is.EqualTo(valid));
}
