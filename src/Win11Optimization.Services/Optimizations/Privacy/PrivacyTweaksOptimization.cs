using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Privacy;

/// <summary>
/// Отключение рекламы, отслеживания активности и обратной связи.
/// 
/// Группа настроек приватности, не влияющих на функциональность системы:
/// - Advertising ID — уникальный идентификатор для таргетированной рекламы
/// - Activity History — история действий (Timeline) и синхронизация
/// - Feedback — запросы обратной связи от Microsoft
/// - App Launch Tracking — отслеживание запуска приложений для меню Пуск
/// 
/// Риск: Low — эти функции не влияют на стабильность системы.
/// </summary>
public sealed class PrivacyTweaksOptimization : IOptimization
{
    private readonly IBackupManager _backup;
    private readonly ILogger<PrivacyTweaksOptimization> _logger;

    private static readonly (RegistryKey BaseKey, string SubKey, string Name, int Value, string Desc)[] Changes =
    [
        // ── Advertising ID ──────────────────────────────
        // Отключает уникальный рекламный идентификатор пользователя
        (Registry.LocalMachine,
         @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
         "DisabledByGroupPolicy", 1,
         "Advertising ID отключён"),

        // ── Activity History / Timeline ─────────────────
        // Отключает сбор и публикацию истории действий пользователя
        (Registry.LocalMachine,
         @"SOFTWARE\Policies\Microsoft\Windows\System",
         "EnableActivityFeed", 0,
         "Activity Feed отключён"),

        (Registry.LocalMachine,
         @"SOFTWARE\Policies\Microsoft\Windows\System",
         "PublishUserActivities", 0,
         "Публикация активности отключена"),

        (Registry.LocalMachine,
         @"SOFTWARE\Policies\Microsoft\Windows\System",
         "UploadUserActivities", 0,
         "Загрузка активности отключена"),

        // ── Feedback Notifications ──────────────────────
        // Отключает всплывающие запросы «Оцените Windows»
        (Registry.CurrentUser,
         @"SOFTWARE\Microsoft\Siuf\Rules",
         "NumberOfSIUFInPeriod", 0,
         "Запросы обратной связи отключены"),

        // ── App Launch Tracking ─────────────────────────
        // Отключает отслеживание запускаемых приложений (для «часто используемых» в Пуске)
        (Registry.CurrentUser,
         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
         "Start_TrackProgs", 0,
         "Отслеживание запуска приложений отключено"),
    ];

    private static readonly string[] BackupPaths =
    [
        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\System",
        @"HKCU\SOFTWARE\Microsoft\Siuf\Rules",
        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
    ];

    public OptimizationInfo Info => new(
        Id: "privacy.tweaks",
        Name: "Приватность и отслеживание",
        Description: "Отключает рекламный ID, историю активности, запросы обратной связи и трекинг приложений.",
        Category: OptimizationCategory.Privacy,
        RiskLevel: RiskLevel.Low
    );

    public PrivacyTweaksOptimization(IBackupManager backup, ILogger<PrivacyTweaksOptimization> logger)
    {
        _backup = backup;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        foreach (var (baseKey, subKey, name, expectedValue, _) in Changes)
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
            // 1. Бэкап (ветки могут не существовать — это нормально)
            foreach (var path in BackupPaths)
            {
                try
                {
                    await _backup.BackupRegistryKeyAsync(Info.Id, path, ct);
                }
                catch (InvalidOperationException)
                {
                    _logger.LogDebug("Ветка {Path} не существует, бэкап пропущен", path);
                }
            }

            // 2. Применяем все изменения
            foreach (var (baseKey, subKey, name, value, desc) in Changes)
            {
                try
                {
                    RegistryHelper.SetDword(baseKey, subKey, name, value);
                    _logger.LogInformation("{Desc}: {Key}\\{Name} = {Value}", desc, subKey, name, value);
                }
                catch (Exception ex)
                {
                    warnings.Add($"{desc}: {ex.Message}");
                    _logger.LogWarning(ex, "Ошибка: {Desc}", desc);
                }
            }

            return warnings.Count > 0
                ? OptimizationResult.Success(warnings)
                : OptimizationResult.Success();
        }
        catch (Exception ex)
        {
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
            var latestTime = myBackups[0].CreatedAt;
            var latestSet = myBackups
                .Where(b => (latestTime - b.CreatedAt).TotalSeconds < 60)
                .ToList();

            foreach (var backup in latestSet)
            {
                var result = await _backup.RestoreRegistryKeyAsync(backup.Id, ct);
                if (!result.IsSuccess)
                    warnings.Add($"Откат {backup.Description}: {result.ErrorMessage}");
            }
        }
        else
        {
            // Нет бэкапов — удаляем созданные значения
            foreach (var (baseKey, subKey, name, _, _) in Changes)
            {
                try
                {
                    RegistryHelper.DeleteValue(baseKey, subKey, name);
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
}
