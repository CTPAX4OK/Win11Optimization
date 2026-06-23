using Win11Optimization.Core.Models;

namespace Win11Optimization.Core.Interfaces;
public interface IBackupManager
{
    Task<OptimizationResult> CreateRestorePointAsync(
        string description,
        CancellationToken ct = default);
    Task<BackupEntry> BackupRegistryKeyAsync(
        string optimizationId,
        string registryPath,
        CancellationToken ct = default);
    Task<OptimizationResult> RestoreRegistryKeyAsync(
        string backupId,
        CancellationToken ct = default);
    Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(
        CancellationToken ct = default);
    Task<OptimizationResult> DeleteBackupAsync(
        string backupId,
        CancellationToken ct = default);
}
