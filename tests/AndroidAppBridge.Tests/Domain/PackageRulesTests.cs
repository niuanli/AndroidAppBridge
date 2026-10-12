namespace AndroidAppBridge.Tests.Domain;

[TestFixture]
public class PackageRulesTests
{
    [TestCase("org.example.game", true)]
    [TestCase("ru.buka.shtirlitz_1", true)]
    [TestCase("android", false)]
    [TestCase("org.example;reboot", false)]
    [TestCase("org.example/../x", false)]
    [TestCase(null, false)]
    public void PackageNamesAreValidated(string? value, bool expected) => Assert.That(PackageRules.IsValidName(value), Is.EqualTo(expected));
    [TestCase("main.4.org.example.game.obb", true)]
    [TestCase("nested/file.obb", true)]
    [TestCase("../file.obb", false)]
    [TestCase("/absolute.obb", false)]
    [TestCase("a/../file.obb", false)]
    [TestCase("a//file.obb", false)]
    [TestCase("file;reboot.obb", false)]
    public void ObbPathsStayWithinThePackage(string path, bool expected) => Assert.That(PackageRules.IsSafeObbRelative(path), Is.EqualTo(expected));
    [TestCase("/data/app/~~abc/pkg/base.apk", true)]
    [TestCase("/data/app/pkg/../base.apk", false)]
    [TestCase("/data/app/base.apk;reboot", false)]
    public void RemoteApkPathsAreValidated(string path, bool expected) => Assert.That(PackageRules.IsSafeRemoteApk(path), Is.EqualTo(expected));
}
