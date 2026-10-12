using System.Text.Json;
using System.Text.RegularExpressions;

namespace AndroidAppBridge.Infrastructure;

/// <summary>Validates selected local APK/OBB files and snapshots them into a standard backup.</summary>
public static class PackageImport
{
    public static string[] FindObbs(string apk, string package)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(apk))!;
        var folders = new[] { root, Path.Combine(root, package), Path.Combine(root, "Android", "obb", package) };
        return folders.Where(Directory.Exists).SelectMany(f => Directory.EnumerateFiles(f, "*.obb", SearchOption.TopDirectoryOnly))
            .Where(f => PackageSelectionPolicy.MatchesObb(Path.GetFileName(f), package))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
    }
    public static LocalPackage Inspect(string[] apks, string[] obbs)
    {
        if (apks.Length == 0)
            throw new InvalidDataException("Выберите хотя бы один APK.");
        var infos = apks.Select(ApkManifest.Read).ToArray();
        var info = PackageSelectionPolicy.Validate(infos);
        var files = new List<ImportSource>();
        for (var i = 0; i < apks.Length; i++)
        {
            var split = infos[i].Split;
            if (split.Length > 0 && !Regex.IsMatch(split, @"^[A-Za-z0-9_.-]+$"))
                throw new InvalidDataException("Имя split APK не поддерживается.");
            files.Add(new(apks[i], split.Length == 0 ? "apk/base.apk" : "apk/split_" + split + ".apk"));
        }
        foreach (var obb in obbs)
        {
            var name = Path.GetFileName(obb);
            // Expansion version codes may legitimately differ from the current APK after an update.
            if (!PackageSelectionPolicy.MatchesObb(name, info.Package))
                throw new InvalidDataException("Имя OBB должно соответствовать пакету APK: " + info.Package);
            files.Add(new(obb, "obb/" + name));
        }
        if (files.Select(f => f.Destination).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
            throw new InvalidDataException("APK или OBB выбран повторно.");
        if (files.Any(f => !File.Exists(f.Path) || new FileInfo(f.Path).Length == 0))
            throw new InvalidDataException("Выбранный файл отсутствует или пуст.");
        return new(info, files.ToArray(), Storage.Abis(apks));
    }

    public static async Task<Backup> Save(LocalPackage package, string folder, Action<string, long, long> progress, CancellationToken ct)
    {
        if (Directory.Exists(folder))
            throw new IOException("Папка импорта уже существует.");
        Directory.CreateDirectory(folder);
        var files = new List<SavedFile>();
        foreach (var f in package.Files)
        {
            ct.ThrowIfCancellationRequested();
            var destination = Storage.Resolve(folder, f.Destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            var buffer = new byte[1024 * 1024];
            long copied = 0;
            int read;
            progress(Path.GetFileName(f.Path), 0, input.Length);
            while ((read = await input.ReadAsync(buffer, ct)) != 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                copied += read;
                progress(Path.GetFileName(f.Path), copied, input.Length);
            }
            await output.FlushAsync(ct);
            output.Close();
            files.Add(new(f.Destination, await Task.Run(() => Storage.Hash(destination, ct), ct), copied));
        }
        // Metadata is read again from the snapshot, not trusted from the earlier selection preview.
        var captured = Inspect(files.Where(f => f.Path.StartsWith("apk/")).Select(f => Storage.Resolve(folder, f.Path)).ToArray(), files.Where(f => f.Path.StartsWith("obb/")).Select(f => Storage.Resolve(folder, f.Path)).ToArray());
        if (captured.Info != package.Info)
            throw new IOException("APK изменился во время импорта. Выберите файлы заново.");
        var b = new Backup(captured.Info.Package, captured.Info.Version, captured.Info.MinSdk, captured.Info.TargetSdk, captured.Abis, files.ToArray(), DateTimeOffset.Now.ToString("O"));
        ct.ThrowIfCancellationRequested();
        Storage.WriteManifest(folder, b, ct);
        return b;
    }
}
