namespace AndroidAppBridge.Application;

/// <summary>GRASP controller for backup, import and installation. All external operations use injected ports.</summary>
public sealed class TransferService(IDeviceGateway devices, IBackupRepository backups, ILocalPackageImporter importer)
{
    static void Stage(IProgress<TransferUpdate>? events, string text, string detail = "", int? percent = null) => events?.Report(new(TransferUpdateKind.Stage, text, detail, percent));
    static void Log(IProgress<TransferUpdate>? events, string text) => events?.Report(new(TransferUpdateKind.Log, text));
    static IProgress<FileTransfer> FileProgress(IProgress<TransferUpdate>? events, string title) => new DelegateProgress<FileTransfer>(f => Stage(events, title,
        $"{f.Name}  •  {f.Bytes / 1048576.0:F1} / {f.Total / 1048576.0:F1} МБ", f.Total > 0 ? (int)Math.Clamp((double)f.Bytes / f.Total * 100, 0, 100) : null));
    public Task<Device[]> GetDevicesAsync(CancellationToken ct) => devices.GetDevicesAsync(ct);
    public Task<string[]> GetAppsAsync(string serial, CancellationToken ct) => devices.GetAppsAsync(serial, ct);
    public Task<Backup> OpenBackupAsync(string folder, CancellationToken ct) => backups.LoadAsync(folder, ct);

    public async Task<BackupLocation> ImportAsync(string[] apks, string[] obbs, IProgress<TransferUpdate>? events, CancellationToken ct)
    {
        Stage(events, "Проверка локальных файлов", "Определение пакета APK и проверка имён OBB.");
        var selected = await Task.Run(() => importer.Inspect(apks, obbs), ct);
        var folder = backups.NewFolder(selected.Info.Package, "import");
        var manifest = await importer.SaveAsync(selected, folder, FileProgress(events, "Импорт локальных файлов"), ct);
        Log(events, "Импорт завершён: " + manifest.Package + ". OBB: " + obbs.Length);
        return new(manifest, folder);
    }

    public async Task<BackupLocation> ExportAsync(string serial, string package, bool includeObb, IProgress<TransferUpdate>? events, CancellationToken ct)
    {
        Stage(events, "Подготовка резервной копии", "Определение версии и состава APK.");
        if (!PackageRules.IsValidName(package))
            throw new InvalidDataException("Некорректное имя пакета APK.");
        var before = await devices.GetPackageAsync(serial, package, ct);
        if (before.ApkPaths.Length == 0 || before.ApkPaths.Any(p => !PackageRules.IsSafeRemoteApk(p))
            || before.ApkPaths.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != before.ApkPaths.Length)
            throw new InvalidDataException("Не удалось получить безопасный список APK.");
        var folder = backups.NewFolder(package, "device");
        Log(events, $"Копирование {package} → {folder}");
        foreach (var path in before.ApkPaths)
        {
            ct.ThrowIfCancellationRequested();
            Log(events, "APK: " + Path.GetFileName(path));
            Log(events, await devices.PullAsync(serial, path, backups.CreateDestination(folder, "apk/" + Path.GetFileName(path)), FileProgress(events, "Копирование файла"), ct));
        }
        if (includeObb)
        {
            string[] obbs;
            try
            {
                obbs = await devices.GetObbPathsAsync(serial, package, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException) { obbs = []; Log(events, "OBB недоступны или отсутствуют: " + e.Message); }
            var prefix = "/sdcard/Android/obb/" + package + "/";
            foreach (var remote in obbs)
            {
                if (!remote.StartsWith(prefix, StringComparison.Ordinal) || !PackageRules.IsSafeObbRelative(remote[prefix.Length..]))
                    throw new InvalidDataException("Необычное имя OBB-файла; копирование остановлено.");
                Log(events, await devices.PullAsync(serial, remote, backups.CreateDestination(folder, "obb/" + remote[prefix.Length..]), FileProgress(events, "Копирование файла"), ct));
            }
        }
        var after = await devices.GetPackageAsync(serial, package, ct);
        if (before.Info != after.Info || !before.ApkPaths.Order().SequenceEqual(after.ApkPaths.Order()))
            throw new IOException("Приложение обновилось во время копирования. Повторите экспорт.");
        Stage(events, "Проверка целостности", "Вычисление SHA-256 скопированных файлов.");
        var manifest = await backups.SealAsync(folder, before.Info, ct);
        Log(events, "Копия готова. Закрытые данные, сохранения и учётные записи не скопированы. APK сохранены с оригинальной подписью.");
        return new(manifest, folder);
    }

    public async Task<InstallationPlan> PrepareInstallAsync(string serial, string folder, IProgress<TransferUpdate>? events, CancellationToken ct)
    {
        Stage(events, "Проверка перед установкой", "Целостность копии и совместимость устройства.");
        Log(events, "Проверка резервной копии…");
        var backup = await backups.LoadAsync(folder, ct);
        var device = await devices.GetDeviceInfoAsync(serial, ct);
        var error = CompatibilityPolicy.Check(backup, device);
        if (error != null)
            throw new InvalidOperationException(error);
        foreach (var obb in backup.Files.Where(f => f.Path.StartsWith("obb/", StringComparison.Ordinal)))
            if (!PackageRules.IsSafeObbRelative(obb.Path[4..]))
                throw new InvalidDataException("Необычное имя OBB; APK установлены, данные не перенесены.");
        Log(events, $"Устройство: API {device.Sdk}, {string.Join(", ", device.Abis)}, страницы {device.PageSize} байт. Это предварительная проверка; запуск всё равно нужно проверить.");
        return new(serial, new(backup, folder), device, await devices.IsInstalledAsync(serial, backup.Package, ct));
    }

    // The view owns confirmation. Executing a plan never opens dialogs or reads UI selection state.
    public async Task<Backup> InstallAsync(InstallationPlan plan, IProgress<TransferUpdate>? events, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var b = await backups.LoadAsync(plan.Backup.Folder, ct);
        if (!CompatibilityPolicy.SameFiles(plan.Backup.Manifest, b))
            throw new IOException("Копия изменилась после подтверждения установки. Откройте её заново.");
        Stage(events, "Установка — проверьте экран устройства", "Подтвердите запрос установки. Android не сообщает процент этого этапа.");
        var apks = b.Files.Where(f => f.Path.StartsWith("apk/", StringComparison.Ordinal)).Select(f => backups.Resolve(plan.Backup.Folder, f.Path)).ToArray();
        Log(events, await devices.InstallAsync(plan.Serial, apks, plan.ReplaceExisting, ct));
        Log(events, "APK установлены.");
        try
        {
            foreach (var file in b.Files.Where(f => f.Path.StartsWith("obb/", StringComparison.Ordinal)))
            {
                var relative = file.Path[4..];
                if (!PackageRules.IsSafeObbRelative(relative))
                    throw new InvalidDataException("Недопустимый путь OBB.");
                Stage(events, "Передача игровых данных", $"{Path.GetFileName(relative)}  •  {file.Size / 1048576.0:F1} МБ");
                Log(events, await devices.PushObbAsync(plan.Serial, backups.Resolve(plan.Backup.Folder, file.Path), "/sdcard/Android/obb/" + b.Package + "/" + relative, ct));
            }
        }
        catch (OperationCanceledException) { Log(events, "APK уже установлены. Передача OBB отменена."); throw; }
        catch (Exception e) { throw new PartialTransferException("APK установлены, но передача OBB не завершена: " + e.Message, e); }
        Log(events, "Перенос завершён. Нажмите «Запустить приложение» и проверьте работу на устройстве.");
        return b;
    }

    public async Task LaunchAsync(string serial, string package, IProgress<TransferUpdate>? events, CancellationToken ct)
    {
        Stage(events, "Запуск приложения", "Результат будет виден на экране устройства.");
        Log(events, await devices.LaunchAsync(serial, package, ct));
        Log(events, "Android подтвердил запуск Activity. Проверьте изображение, звук и управление на экране устройства.");
    }
}
