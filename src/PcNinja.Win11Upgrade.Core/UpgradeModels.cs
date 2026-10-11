namespace PcNinja.Win11Upgrade.Core;

public sealed record MountedSource(string Root, string SetupPath);

public sealed record UpgradeResult(int ExitCode, string SetupPath);

public sealed record DownloadProgress(
    string DestinationPath,
    long BytesReceived,
    long? TotalBytes,
    int? Percentage);

public sealed record DownloadResult(
    string IsoPath,
    long BytesReceived,
    long? ExpectedBytes);

public sealed record StorageCheck(
    bool IsValid,
    string RootPath,
    string FileSystem,
    long AvailableBytes,
    long RequiredBytes,
    string Message);

public sealed record StorageLocation(
    string RootPath,
    string VolumeLabel,
    DriveType DriveType,
    string FileSystem,
    long AvailableBytes)
{
    public override string ToString()
    {
        var label = string.IsNullOrWhiteSpace(VolumeLabel) ? "Local disk" : VolumeLabel;
        return $"{RootPath}  {label} - {UpgradeEngine.FormatBytes(AvailableBytes)} free ({FileSystem})";
    }
}
