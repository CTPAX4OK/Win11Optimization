namespace Win11Optimization.Core.Models;

/// <summary>
/// Тип бэкапа — определяет, какой именно ресурс был сохранён.
/// </summary>
public enum BackupType
{
    /// <summary>Экспорт ветки реестра в .reg файл.</summary>
    RegistryKey,

    /// <summary>Точка восстановления системы Windows.</summary>
    SystemRestorePoint,

    /// <summary>Копия файла (конфигурация, системный файл).</summary>
    File
}

/// <summary>
/// Запись о созданном бэкапе — хранит метаданные для отката.
/// </summary>
/// <param name="Id">Уникальный идентификатор бэкапа (GUID).</param>
/// <param name="OptimizationId">ID оптимизации, создавшей бэкап.</param>
/// <param name="Description">Описание того, что было сохранено.</param>
/// <param name="FilePath">Путь к файлу бэкапа (.reg, копия файла). Null для точек восстановления.</param>
/// <param name="CreatedAt">Время создания бэкапа (UTC).</param>
/// <param name="Type">Тип бэкапа.</param>
public sealed record BackupEntry(
    string Id,
    string OptimizationId,
    string Description,
    string? FilePath,
    DateTime CreatedAt,
    BackupType Type
);
