using System.Text.RegularExpressions;

namespace AndroidAppBridge.Infrastructure;

/// <summary>Translates Android queries and operations into ADB commands and domain values.</summary>
public sealed class AdbDeviceGateway(IAdbClient client) : IDeviceGateway
{
    public async Task<Device[]> GetDevicesAsync(CancellationToken ct)
    {
        var text = await client.RunAsync(null, ct, "devices", "-l");
        return text.Split('\n').Select(s => Regex.Match(s.Trim(), @"^(\S+)\s+(device|unauthorized|offline)\b(.*)$")).Where(m => m.Success)
            .Select(m => new Device(m.Groups[1].Value, m.Groups[2].Value, Regex.Match(m.Groups[3].Value, @"model:(\S+)") is var model && model.Success ? model.Groups[1].Value.Replace('_', ' ') : "Android")).ToArray();
    }
    public async Task<string[]> GetAppsAsync(string serial, CancellationToken ct)
    {
        var text = await client.RunAsync(serial, ct, "shell", "pm", "list", "packages", "-3");
        return text.Split('\n').Select(s => s.Trim()).Where(s => s.StartsWith("package:")).Select(s => s[8..]).Where(PackageRules.IsValidName).Distinct().Order().ToArray();
    }
    public async Task<InstalledPackage> GetPackageAsync(string serial, string package, CancellationToken ct)
    {
        if (!PackageRules.IsValidName(package))
            throw new InvalidDataException("Некорректное имя пакета APK.");
        var paths = (await client.RunAsync(serial, ct, "shell", "pm", "path", package)).Split('\n').Select(s => s.Trim()).Where(s => s.StartsWith("package:")).Select(s => s[8..]).ToArray();
        var dump = await client.RunAsync(serial, ct, "shell", "dumpsys", "package", package);
        int Field(string name) => int.TryParse(Regex.Match(dump, name + @"=(\d+)").Groups[1].Value, out var n) ? n : 0;
        long.TryParse(Regex.Match(dump, @"versionCode=(\d+)").Groups[1].Value, out var versionCode);
        var info = new ApkInfo(package, Regex.Match(dump, @"versionName=([^\r\n]+)").Groups[1].Value.Trim(), versionCode, Field("minSdk"), Field("targetSdk"), "");
        return new(info, paths);
    }
    public async Task<string[]> GetObbPathsAsync(string serial, string package, CancellationToken ct) => (await client.RunAsync(serial, ct, "shell", "find", "/sdcard/Android/obb/" + package, "-type", "f")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public async Task<string> PullAsync(string serial, string remote, string local, IProgress<FileTransfer>? progress, CancellationToken ct)
    {
        var sizeText = await client.RunAsync(serial, ct, "shell", "stat", "-c", "%s", remote);
        long.TryParse(sizeText.Trim(), out var total);
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new(Path.GetFileName(remote), 0, total));
            using var polling = CancellationTokenSource.CreateLinkedTokenSource(ct);
            async Task PollAsync()
            {
                try
                {
                    while (true)
                    {
                        await Task.Delay(150, polling.Token).ConfigureAwait(false);
                        try
                        {
                            progress?.Report(new(Path.GetFileName(remote), File.Exists(local) ? new FileInfo(local).Length : 0, total));
                        }
                        catch (IOException) { }
                    }
                }
                catch (OperationCanceledException) when (polling.IsCancellationRequested) { }
            }
            var poll = PollAsync();
            try
            {
                var result = await client.RunAsync(serial, ct, "pull", remote, local).ConfigureAwait(false);
                await polling.CancelAsync();
                await poll.ConfigureAwait(false);
                progress?.Report(new(Path.GetFileName(remote), total, total));
                return result;
            }
            catch (Exception e) when (e is not OperationCanceledException && attempt < 3)
            {
                await polling.CancelAsync();
                await poll.ConfigureAwait(false);
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }
            finally { await polling.CancelAsync(); await poll.ConfigureAwait(false); }
        }
    }
    public async Task<DeviceInfo> GetDeviceInfoAsync(string serial, CancellationToken ct)
    {
        var sdk = int.Parse((await client.RunAsync(serial, ct, "shell", "getprop", "ro.build.version.sdk")).Trim());
        var abis = (await client.RunAsync(serial, ct, "shell", "getprop", "ro.product.cpu.abilist")).Trim().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int.TryParse((await client.RunAsync(serial, ct, "shell", "getconf", "PAGE_SIZE")).Trim(), out var pageSize);
        var space = await client.RunAsync(serial, ct, "shell", "df", "-k", "/data");
        var line = space.Split('\n').LastOrDefault(s => Regex.IsMatch(s.Trim(), @"^/\S+\s+\d+\s+\d+\s+\d+"));
        long? available = null;
        if (line != null && long.TryParse(Regex.Split(line.Trim(), @"\s+")[3], out var kb))
            available = checked(kb * 1024);
        return new(sdk, abis, pageSize, available);
    }
    public async Task<bool> IsInstalledAsync(string serial, string package, CancellationToken ct) => (await client.RunAsync(serial, ct, "shell", "pm", "list", "packages", package)).Split('\n').Any(s => s.Trim() == "package:" + package);
    public Task<string> InstallAsync(string serial, string[] apks, bool replace, CancellationToken ct)
    {
        if (apks.Length == 0)
            throw new InvalidDataException("Нет APK для установки.");
        return client.RunAsync(serial, ct, new[] { "install-multiple" }.Concat(replace ? ["-r"] : Array.Empty<string>()).Concat(apks).ToArray());
    }
    public async Task<string> PushObbAsync(string serial, string local, string remote, CancellationToken ct)
    {
        await client.RunAsync(serial, ct, "shell", "mkdir", "-p", remote[..remote.LastIndexOf('/')]);
        return await client.RunAsync(serial, ct, "push", local, remote);
    }
    public async Task<string> LaunchAsync(string serial, string package, CancellationToken ct)
    {
        var resolved = await client.RunAsync(serial, ct, "shell", "cmd", "package", "resolve-activity", "--brief", package);
        var component = resolved.Split('\n').Select(s => s.Trim()).LastOrDefault(s => s.StartsWith(package + "/", StringComparison.Ordinal) && Regex.IsMatch(s, @"^[A-Za-z0-9_.$/]+$")) ?? throw new InvalidOperationException("Нет запускаемой Activity. Проверьте, установлено ли приложение.");
        var result = await client.RunAsync(serial, ct, "shell", "am", "start", "-W", "-n", component);
        if (result.Contains("Error:") || !result.Contains("Status: ok"))
            throw new IOException("Android не подтвердил запуск приложения.");
        return result;
    }
}
