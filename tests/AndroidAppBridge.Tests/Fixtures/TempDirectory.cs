using System.IO.Compression;

namespace AndroidAppBridge.Tests.Fixtures;

sealed class TempDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "AndroidAppBridge-tests-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Root);
    public string FilePath(string relative) => Path.GetFullPath(Path.Combine(Root, relative));
    public string Apk(string name = "game.apk", string package = "org.example.game", string split = "", bool utf8 = true, string? abi = null)
    {
        var path = FilePath(name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var entry = zip.CreateEntry("AndroidManifest.xml").Open())
            entry.Write(ManifestFixture.Manifest(utf8, package, split));
        if (abi != null)
            zip.CreateEntry("lib/" + abi + "/fixture.so");
        return path;
    }
    public string Obb(string name = "main.4.org.example.game.obb")
    {
        var path = FilePath(name);
        File.WriteAllText(path, "expansion fixture");
        return path;
    }
    public void Dispose()
    {
        var full = Path.GetFullPath(Root);
        if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("AndroidAppBridge-tests-"))
            throw new InvalidOperationException("Unexpected fixture directory.");
        Directory.Delete(Root, true);
    }
}
