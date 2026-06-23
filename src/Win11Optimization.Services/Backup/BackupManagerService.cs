using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;

namespace Win11Optimization.Services.Backup;
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
        _backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
        _indexPath = Path.Combine(_backupDir, "index.json");

        Directory.CreateDirectory(_backupDir);
        _logger.LogDebug("Backup directory: {Dir}", _backupDir);
    }

    private async Task EnableSystemRestoreAsync(CancellationToken ct)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"Enable-ComputerRestore -Drive 'C:\'\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.Start();
            await process.WaitForExitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Не удалось автоматически включить System Restore: {Msg}", ex.Message);
        }
    }

    public async Task<OptimizationResult> CreateRestorePointAsync(
        string description,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Создание точки восстановления: {Description}", description);

        await EnableSystemRestoreAsync(ct);

        try
        {
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
            var entry = new BackupEntry(
                Id: Guid.NewGuid().ToString("N"),
                OptimizationId: "system",
                Description: $"Точка восстановления: {description}",
                FilePath: null,
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
            _logger.LogError(ex, "srclient.dll не найден — System Restore API недоступен");
            return OptimizationResult.Failure("srclient.dll не найден. System Restore недоступен в этой сборке Windows.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при создании точки восстановления");
            return OptimizationResult.Failure($"Исключение: {ex.Message}");
        }
    }
    public async Task<BackupEntry> BackupRegistryKeyAsync(
        string optimizationId,
        string registryPath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Бэкап ключа реестра: {Path}", registryPath);

        var id = Guid.NewGuid().ToString("N");
        var fileName = $"{id}.reg";
        var filePath = Path.Combine(_backupDir, fileName);
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
    public async Task<OptimizationResult> RestoreRegistryKeyAsync(
        string backupId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Восстановление бэкапа реестра: {Id}", backupId);
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
    public async Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken ct = default)
    {
        return await LoadIndexAsync(ct);
    }
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
            if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
            {
                File.Delete(entry.FilePath);
                _logger.LogDebug("Файл бэкапа удалён: {Path}", entry.FilePath);
            }
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
    private async Task WriteIndexAsync(List<BackupEntry> entries, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await File.WriteAllTextAsync(_indexPath, json, ct);
    }

    public void Dispose()
    {
        _indexLock.Dispose();
    }
}
