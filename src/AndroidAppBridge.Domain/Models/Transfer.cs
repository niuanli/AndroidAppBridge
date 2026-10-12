namespace AndroidAppBridge.Domain;

public record DeviceInfo(int Sdk, string[] Abis, int PageSize, long? FreeBytes);
public record InstalledPackage(ApkInfo Info, string[] ApkPaths);
public record BackupLocation(Backup Manifest, string Folder);
public record InstallationPlan(string Serial, BackupLocation Backup, DeviceInfo Device, bool ReplaceExisting);
public record FileTransfer(string Name, long Bytes, long Total);
public enum TransferUpdateKind
{
    Stage, Log
}
public record TransferUpdate(TransferUpdateKind Kind, string Text, string Detail = "", int? Percent = null);

/// <summary>Represents a failure after APK installation, when the OBB phase cannot be rolled back safely.</summary>
public sealed class PartialTransferException(string message, Exception inner) : Exception(message, inner);
