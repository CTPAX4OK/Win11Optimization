using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Privacy;

/// <summary>
/// Отключение телеметрии и диагностических данных Windows 11.
/// 
/// Windows собирает данные о работе системы и отправляет их на серверы Microsoft.
/// Уровни телеметрии: Security (0), Basic (1), Enhanced (2), Full (3).
/// Эта оптимизация устанавливает уровень 0 (Security) — минимальный.
/// 
/// Риск: Medium — может повлиять на работу Windows Update и некоторых
/// функций, зависящих от диагностических данных.
/// 
/// Реестр:
/// - HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection
/// - HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection
/// - HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy
/// </summary>
public sealed class TelemetryOptimization : IOptimization
{
    private readonly IBackupManager _backup;
    private readonly ILogger<TelemetryOptimization> _logger;

    /// <summary>
    /// Все registry-изменения для этой оптимизации.
    /// Используется для Apply, IsApplied и Rollback (удаление при отсутствии бэкапа).
    /// </summary>
    private static readonly (RegistryKey BaseKey, string SubKey, string Name, int Value)[] Changes =
    [
        // Политика: уровень телеметрии = 0 (Security)
        (Registry.LocalMachine,
         @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
         "AllowTelemetry", 0),

        // Системная настройка: уровень телеметрии = 0
        (Registry.LocalMachine,
         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
         "AllowTelemetry", 0),

        // Максимально допустимый уровень = 0
        (Registry.LocalMachine,
         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
         "MaxTelemetryAllowed", 0),

        // Отключение «персонализированных рекомендаций» на основе диагностики
        (Registry.CurrentUser,
         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy",
         "TailoredExperiencesWithDiagnosticDataEnabled", 0),
    ];

    /// <summary>Пути для бэкапа (уникальные ветки реестра).</summary>
    private static readonly string[] BackupPaths =
    [
        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy",
    ];

    public OptimizationInfo Info => new(
        Id: "privacy.telemetry_disable",
        Name: "Отключение телеметрии",
        Description: "Устанавливает минимальный уровень диагностических данных (Security).",
        Category: OptimizationCategory.Privacy,
        RiskLevel: RiskLevel.Medium
    );

    public TelemetryOptimization(IBackupManager backup, ILogger<TelemetryOptimization> logger)
    {
        _backup = backup;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        foreach (var (baseKey, subKey, name, expectedValue) in Changes)
        {
            var current = RegistryHelper.GetDword(baseKey, subKey, name);
            if (current != expectedValue)
                return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();

        try
        {
            // 1. Бэкап каждой ветки (если она существует)
            foreach (var path in BackupPaths)
            {
                await TryBackupAsync(path, warnings, ct);
            }

            // 2. Применяем изменения
            foreach (var (baseKey, subKey, name, value) in Changes)
            {
                try
                {
                    RegistryHelper.SetDword(baseKey, subKey, name, value);
                    _logger.LogInformation("Установлено: {Key}\\{Name} = {Value}", subKey, name, value);
                }
                catch (Exception ex)
                {
                    warnings.Add($"{name}: {ex.Message}");
                    _logger.LogWarning(ex, "Ошибка установки {Name}", name);
                }
            }

            _logger.LogInformation("Телеметрия отключена");

            return warnings.Count > 0
                ? OptimizationResult.Success(warnings)
                : OptimizationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка отключения телеметрии");
            return OptimizationResult.Failure($"Ошибка: {ex.Message}", warnings);
        }
    }

    /// <inheritdoc />
    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var backups = await _backup.ListBackupsAsync(ct);

        var myBackups = backups
            .Where(b => b.OptimizationId == Info.Id && b.Type == BackupType.RegistryKey)
            .OrderByDescending(b => b.CreatedAt)
            .ToList();

        if (myBackups.Count > 0)
        {
            // Восстанавливаем из бэкапов (берём последний набор — в пределах 60 секунд)
            var latestTime = myBackups[0].CreatedAt;
            var latestSet = myBackups
                .Where(b => (latestTime - b.CreatedAt).TotalSeconds < 60)
                .ToList();

            foreach (var backup in latestSet)
            {
                var result = await _backup.RestoreRegistryKeyAsync(backup.Id, ct);
                if (!result.IsSuccess)
                    warnings.Add($"Откат {backup.Description}: {result.ErrorMessage}");
                else
                    _logger.LogInformation("Восстановлено: {Desc}", backup.Description);
            }
        }
        else
        {
            // Нет бэкапов — удаляем значения, которые мы создали
            _logger.LogWarning("Бэкапы не найдены, удаляем значения вручную");
            foreach (var (baseKey, subKey, name, _) in Changes)
            {
                try
                {
                    RegistryHelper.DeleteValue(baseKey, subKey, name);
                    _logger.LogInformation("Удалено: {Key}\\{Name}", subKey, name);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Удаление {name}: {ex.Message}");
                }
            }
        }

        return warnings.Count > 0
            ? OptimizationResult.Success(warnings)
            : OptimizationResult.Success();
    }

    // ── Вспомогательные методы ──────────────────────────

    /// <summary>
    /// Пытается создать бэкап ветки реестра. Если ветка не существует —
    /// добавляет warning, но не прерывает операцию.
    /// </summary>
    private async Task TryBackupAsync(string regPath, List<string> warnings, CancellationToken ct)
    {
        try
        {
            await _backup.BackupRegistryKeyAsync(Info.Id, regPath, ct);
        }
        catch (InvalidOperationException ex)
        {
            warnings.Add($"Бэкап {regPath}: ветка не существует (будет создана)");
            _logger.LogDebug(ex, "Ветка {Path} не существует, бэкап пропущен", regPath);
        }
    }
}
