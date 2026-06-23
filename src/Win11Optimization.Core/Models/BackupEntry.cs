namespace Win11Optimization.Core.Models;
public enum BackupType
{
    RegistryKey,
    SystemRestorePoint,
    File
}
public sealed record BackupEntry(
    string Id,
    string OptimizationId,
    string Description,
    string? FilePath,
    DateTime CreatedAt,
    BackupType Type
);
