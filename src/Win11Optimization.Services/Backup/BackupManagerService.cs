using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;

namespace Win11Optimization.Services.Backup;

/// <summary>
/// Реализация IBackupManager — управление бэкапами реестра и точками восстановления.
/// 
/// Хранение:
/// - Файлы бэкапов: {AppDir}/backups/*.reg
/// - Индекс бэкапов: {AppDir}/backups/index.json
/// 
/// Потокобезопасность:
/// - SemaphoreSlim для доступа к index.json
/// - Все публичные методы async-safe
/// 
/// Зависимости:
/// - reg.exe (встроен в Windows) — экспорт/импорт реестра
/// - srclient.dll (System Restore API) — точки восстановления
/// </summary>
public sealed class BackupManagerService : IBackupManager, IDisposable
{
    private readonly string _backupDir;
    private readonly string _indexPath;
    private readonly ILogger<BackupManagerService> _logger;
    private readonly SemaphoreSlim _indexLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BackupManagerService(ILogger<BackupManagerService> logger)
    {
        _logger = logger;

        // Бэкапы хранятся рядом с исполняемым файлом
        _backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
        _indexPath = Path.Combine(_backupDir, "index.json");

        Directory.CreateDirectory(_backupDir);
        _logger.LogDebug("Backup directory: {Dir}", _backupDir);
    }

    // ═══════════════════════════════════════════════════════
    //  Точки восстановления (P/Invoke → srclient.dll)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// Использует SRSetRestorePointW из srclient.dll (P/Invoke).
    /// 
    /// Возможные причины неудачи:
    /// - Служба "System Restore" (srservice) отключена → nStatus = 1058
    /// - Защита системы выключена для всех дисков
    /// - Недостаточно прав (не администратор)
    /// 
    /// НЕ считаем фатальной ошибкой — оптимизация может продолжиться
    /// с бэкапом реестра как единственным механизмом отката.
    /// </remarks>
    public async Task<OptimizationResult> CreateRestorePointAsync(
        string description,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Создание точки восстановления: {Description}", description);

        try
        {
            // Формируем описание (максимум 255 символов + \0)
            var rpDescription = $"Win11Optimization: {description}";
            if (rpDescription.Length > 255)
                rpDescription = rpDescription[..255];

            var rpInfo = new NativeMethods.RESTOREPOINTINFO
            {
                dwEventType = NativeMethods.BEGIN_SYSTEM_CHANGE,
                dwRestorePtType = NativeMethods.MODIFY_SETTINGS,
                llSequenceNumber = 0,
                szDescription = rpDescription
            };

            // P/Invoke вызов — синхронный, но быстрый (~100ms)
            var success = NativeMethods.SRSetRestorePointW(ref rpInfo, out var status);

            if (!success || status.nStatus != 0)
            {
                var errorMsg = status.nStatus switch
                {
                    1058 => "Служба System Restore отключена (srservice). Включите защиту системы.",
                    1062 => "Служба System Restore не запущена.",
                    _ => $"Ошибка System Restore API (код: {status.nStatus})"
                };

                _logger.LogWarning("Точка восстановления не создана: {Error}", errorMsg);
                return OptimizationResult.Failure(errorMsg);
            }

            // Сохраняем запись в индекс
            var entry = new BackupEntry(
                Id: Guid.NewGuid().ToString("N"),
                OptimizationId: "system",
                Description: $"Точка восстановления: {description}",
                FilePath: null, // У точки восстановления нет файла
                CreatedAt: DateTime.UtcNow,
                Type: BackupType.SystemRestorePoint
            );

            await SaveEntryAsync(entry, ct);

            _logger.LogInformation(
                "Точка восстановления создана (sequence: {Seq})",
                status.llSequenceNumber);

            return OptimizationResult.Success();
        }
        catch (DllNotFoundException ex)
        {
            // srclient.dll отсутствует — крайне редкий случай
            _logger.LogError(ex, "srclient.dll не найден — System Restore API недоступен");
            return OptimizationResult.Failure("srclient.dll не найден. System Restore недоступен в этой сборке Windows.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при создании точки восстановления");
            return OptimizationResult.Failure($"Исключение: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Бэкап реестра (reg.exe export / import)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// Экспорт через reg.exe:
    ///   reg export "HKLM\SOFTWARE\..." "C:\path\backup.reg" /y
    /// 
    /// Выбран reg.exe вместо Microsoft.Win32.Registry API потому что:
    /// 1. Экспортирует всю ветку рекурсивно одной командой
    /// 2. Формат .reg — человекочитаемый, можно проверить вручную
    /// 3. Импорт через reg import — атомарная операция
    /// 
    /// Throws InvalidOperationException при ошибке — бэкап является
    /// обязательным условием перед оптимизацией (fail-fast).
    /// </remarks>
    public async Task<BackupEntry> BackupRegistryKeyAsync(
        string optimizationId,
        string registryPath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Бэкап ключа реестра: {Path}", registryPath);

        var id = Guid.NewGuid().ToString("N");
        var fileName = $"{id}.reg";
        var filePath = Path.Combine(_backupDir, fileName);

        // Запускаем reg.exe export
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"""export "{registryPath}" "{filePath}" /y""",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        // Читаем stderr до WaitForExit чтобы избежать deadlock на буфере
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "reg export завершился с ошибкой (exit: {Code}): {Error}",
                process.ExitCode, stderr.Trim());

            throw new InvalidOperationException(
                $"Не удалось экспортировать ключ реестра '{registryPath}': {stderr.Trim()}");
        }

        // Проверяем, что файл действительно создан
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"reg export завершился успешно, но файл '{filePath}' не найден");
        }

        var entry = new BackupEntry(
            Id: id,
            OptimizationId: optimizationId,
            Description: $"Реестр: {registryPath}",
            FilePath: filePath,
            CreatedAt: DateTime.UtcNow,
            Type: BackupType.RegistryKey
        );

        await SaveEntryAsync(entry, ct);

        var fileSize = new FileInfo(filePath).Length;
        _logger.LogInformation(
            "Бэкап создан: {Id} → {File} ({Size} bytes)",
            id, fileName, fileSize);

        return entry;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Импорт через reg.exe:
    ///   reg import "C:\path\backup.reg"
    /// 
    /// Важно: reg import ПЕРЕЗАПИСЫВАЕТ существующие значения,
    /// но НЕ удаляет ключи, которых нет в .reg файле.
    /// Это безопасное поведение для наших целей.
    /// </remarks>
    public async Task<OptimizationResult> RestoreRegistryKeyAsync(
        string backupId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Восстановление бэкапа реестра: {Id}", backupId);

        // Ищем бэкап в индексе
        var entries = await LoadIndexAsync(ct);
        var entry = entries.FirstOrDefault(e => e.Id == backupId);

        if (entry is null)
            return OptimizationResult.Failure($"Бэкап '{backupId}' не найден в индексе");

        if (entry.Type != BackupType.RegistryKey)
            return OptimizationResult.Failure(
                $"Бэкап '{backupId}' не является ключом реестра (тип: {entry.Type})");

        if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
            return OptimizationResult.Failure(
                $"Файл бэкапа не найден: {entry.FilePath ?? "(null)"}");

        // Запускаем reg.exe import
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"""import "{entry.FilePath}" """,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            _logger.LogError("reg import завершился с ошибкой: {Error}", stderr.Trim());
            return OptimizationResult.Failure($"Не удалось импортировать бэкап: {stderr.Trim()}");
        }

        _logger.LogInformation("Бэкап восстановлен: {Id} ({Desc})", backupId, entry.Description);
        return OptimizationResult.Success();
    }

    // ═══════════════════════════════════════════════════════
    //  Управление индексом бэкапов
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken ct = default)
    {
        return await LoadIndexAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OptimizationResult> DeleteBackupAsync(
        string backupId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Удаление бэкапа: {Id}", backupId);

        await _indexLock.WaitAsync(ct);
        try
        {
            var entries = await LoadIndexUnsafeAsync(ct);
            var entry = entries.FirstOrDefault(e => e.Id == backupId);

            if (entry is null)
                return OptimizationResult.Failure($"Бэкап '{backupId}' не найден");

            // Удаляем файл бэкапа (если есть)
            if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
            {
                File.Delete(entry.FilePath);
                _logger.LogDebug("Файл бэкапа удалён: {Path}", entry.FilePath);
            }

            // Убираем запись из индекса
            var updated = entries.Where(e => e.Id != backupId).ToList();
            await WriteIndexAsync(updated, ct);

            _logger.LogInformation("Бэкап удалён: {Id}", backupId);
            return OptimizationResult.Success();
        }
        finally
        {
            _indexLock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Private: работа с index.json
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Добавляет запись в индекс бэкапов (потокобезопасно).
    /// </summary>
    private async Task SaveEntryAsync(BackupEntry entry, CancellationToken ct)
    {
        await _indexLock.WaitAsync(ct);
        try
        {
            var entries = await LoadIndexUnsafeAsync(ct);
            entries.Add(entry);
            await WriteIndexAsync(entries, ct);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Загружает индекс бэкапов (потокобезопасно — захватывает lock).
    /// Используйте этот метод из публичных методов.
    /// </summary>
    private async Task<List<BackupEntry>> LoadIndexAsync(CancellationToken ct)
    {
        await _indexLock.WaitAsync(ct);
        try
        {
            return await LoadIndexUnsafeAsync(ct);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Загружает индекс БЕЗ захвата lock.
    /// Вызывайте ТОЛЬКО из контекста, где lock уже захвачен.
    /// </summary>
    private async Task<List<BackupEntry>> LoadIndexUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(_indexPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_indexPath, ct);
            return JsonSerializer.Deserialize<List<BackupEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Повреждён index.json — создаём новый индекс");
            return [];
        }
    }

    /// <summary>
    /// Записывает индекс в файл. Вызывайте ТОЛЬКО из контекста с lock.
    /// </summary>
    private async Task WriteIndexAsync(List<BackupEntry> entries, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await File.WriteAllTextAsync(_indexPath, json, ct);
    }

    // ═══════════════════════════════════════════════════════
    //  IDisposable
    // ═══════════════════════════════════════════════════════

    public void Dispose()
    {
        _indexLock.Dispose();
    }
}
