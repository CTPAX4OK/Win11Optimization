using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.System;
public sealed class VisualEffectsOptimization : IOptimization
{
    private readonly IBackupManager _backup;
    private readonly ILogger<VisualEffectsOptimization> _logger;

    private static readonly (RegistryKey BaseKey, string SubKey, string Name, int Value, string Desc)[] Changes =
    [
        (Registry.CurrentUser,
         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
         "EnableTransparency", 0,
         "Прозрачность отключена"),
        (Registry.CurrentUser,
         @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
         "VisualFXSetting", 2,
         "Установлен пресет 'Обеспечить наилучшее быстродействие'"),
        (Registry.CurrentUser,
         @"Control Panel\Desktop\WindowMetrics",
         "MinAnimate", 0,
         "Анимация сворачивания окон отключена")
    ];

    private static readonly string[] BackupPaths =
    [
        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
        @"HKCU\Control Panel\Desktop\WindowMetrics"
    ];

    public OptimizationInfo Info => new(
        Id: "system.visual_effects",
        Name: "Визуальные эффекты",
        Description: "Отключает ресурсоемкие анимации и прозрачность для максимального быстродействия UI.",
        Category: OptimizationCategory.System,
        RiskLevel: RiskLevel.Low
    );

    public VisualEffectsOptimization(IBackupManager backup, ILogger<VisualEffectsOptimization> logger)
    {
        _backup = backup;
        _logger = logger;
    }

    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        foreach (var (baseKey, subKey, name, expectedValue, _) in Changes)
        {
            var val = baseKey.OpenSubKey(subKey)?.GetValue(name);
            if (val == null) return Task.FromResult(false);

            if (val is int intVal)
            {
                if (intVal != expectedValue) return Task.FromResult(false);
            }
            else if (val is string strVal)
            {
                if (strVal != expectedValue.ToString()) return Task.FromResult(false);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();

        try
        {
            foreach (var path in BackupPaths)
            {
                try
                {
                    await _backup.BackupRegistryKeyAsync(Info.Id, path, ct);
                }
                catch (InvalidOperationException)
                {
                    _logger.LogDebug("Ветка {Path} не существует", path);
                }
            }

            foreach (var (baseKey, subKey, name, value, desc) in Changes)
            {
                try
                {
                    if (subKey.Contains("WindowMetrics"))
                    {
                        using var key = baseKey.OpenSubKey(subKey, writable: true) ?? baseKey.CreateSubKey(subKey);
                        key.SetValue(name, value.ToString(), RegistryValueKind.String);
                    }
                    else
                    {
                        RegistryHelper.SetDword(baseKey, subKey, name, value);
                    }
                    _logger.LogInformation("{Desc}: {Key}\\{Name} = {Value}", desc, subKey, name, value);
                }
                catch (Exception ex)
                {
                    warnings.Add($"{desc}: {ex.Message}");
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
            warnings.Add("Бэкапы не найдены, невозможно откатить визуальные эффекты.");
        }

        return warnings.Count > 0
            ? OptimizationResult.Success(warnings)
            : OptimizationResult.Success();
    }
}
