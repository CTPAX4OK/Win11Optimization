using Win11Optimization.Core.Models;

namespace Win11Optimization.Core.Interfaces;

/// <summary>
/// Управление бэкапами — создание, восстановление и хранение.
/// 
/// Каждая оптимизация использует этот сервис для бэкапа реестра
/// и системных настроек перед внесением изменений.
/// 
/// Бэкапы хранятся локально в папке backups/ рядом с исполняемым файлом.
/// </summary>
public interface IBackupManager
{
    /// <summary>
    /// Создаёт точку восстановления системы через WMI/COM.
    /// Требует прав администратора.
    /// </summary>
    /// <param name="description">Описание точки восстановления.</param>
    /// <param name="ct">Токен отмены.</param>
    Task<OptimizationResult> CreateRestorePointAsync(
        string description,
        CancellationToken ct = default);

    /// <summary>
    /// Экспортирует ветку реестра в .reg файл через reg.exe.
    /// </summary>
    /// <param name="optimizationId">ID оптимизации-владельца.</param>
    /// <param name="registryPath">Полный путь ветки реестра (например, HKLM\SOFTWARE\...).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Запись о созданном бэкапе.</returns>
    Task<BackupEntry> BackupRegistryKeyAsync(
        string optimizationId,
        string registryPath,
        CancellationToken ct = default);

    /// <summary>
    /// Восстанавливает ветку реестра из .reg файла через reg.exe import.
    /// </summary>
    /// <param name="backupId">ID бэкапа для восстановления.</param>
    /// <param name="ct">Токен отмены.</param>
    Task<OptimizationResult> RestoreRegistryKeyAsync(
        string backupId,
        CancellationToken ct = default);

    /// <summary>
    /// Возвращает список всех созданных бэкапов.
    /// </summary>
    Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Удаляет бэкап по ID (файл + запись из индекса).
    /// </summary>
    Task<OptimizationResult> DeleteBackupAsync(
        string backupId,
        CancellationToken ct = default);
}
