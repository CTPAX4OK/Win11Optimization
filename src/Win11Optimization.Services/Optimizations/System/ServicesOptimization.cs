using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.System;
public sealed class ServicesOptimization : IOptimization
{
    private readonly IBackupManager _backup;
    private readonly ILogger<ServicesOptimization> _logger;

    private static readonly (RegistryKey BaseKey, string SubKey, string Name, int Value, string Desc)[] Changes =
    [
        (Registry.LocalMachine,
         @"SYSTEM\CurrentControlSet\Services\SysMain",
         "Start", 4,
         "Служба SysMain (Superfetch) отключена"),
        (Registry.LocalMachine,
         @"SYSTEM\CurrentControlSet\Services\DiagTrack",
         "Start", 4,
         "Служба телеметрии (DiagTrack) отключена"),
        (Registry.LocalMachine,
         @"SYSTEM\CurrentControlSet\Services\dmwappushservice",
         "Start", 4,
         "Служба WAP Push Message Routing отключена")
    ];

    private static readonly string[] BackupPaths =
    [
        @"HKLM\SYSTEM\CurrentControlSet\Services\SysMain",
        @"HKLM\SYSTEM\CurrentControlSet\Services\DiagTrack",
        @"HKLM\SYSTEM\CurrentControlSet\Services\dmwappushservice"
    ];

    public OptimizationInfo Info => new(
        Id: "system.services",
        Name: "Оптимизация фоновых служб",
        Description: "Отключает ресурсоемкие службы: SysMain (Superfetch на SSD) и службы сбора телеметрии.",
        Category: OptimizationCategory.System,
        RiskLevel: RiskLevel.Medium
    );

    public ServicesOptimization(IBackupManager backup, ILogger<ServicesOptimization> logger)
    {
        _backup = backup;
        _logger = logger;
    }

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
                    _logger.LogDebug("Ветка {Path} не существует, бэкап пропущен", path);
                }
            }

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
            warnings.Add("Бэкапы не найдены, невозможно откатить настройки служб.");
        }

        return warnings.Count > 0
            ? OptimizationResult.Success(warnings)
            : OptimizationResult.Success();
    }
}
