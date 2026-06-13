using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Gaming;

/// <summary>
/// Оптимизация Windows 11 для игр.
/// 
/// Включает:
/// 1. Game Mode — приоритет ресурсов для полноэкранных игр
/// 2. Hardware-Accelerated GPU Scheduling — GPU берёт на себя планирование задач
/// 3. Отключение Game DVR — фоновая запись экрана (потребляет GPU/CPU)
/// 4. Отключение Game Bar overlay — оверлей Xbox Game Bar
/// 
/// Примечание: GPU Scheduling требует совместимого GPU (NVIDIA 10xx+, AMD 5000+).
/// Если GPU не поддерживает — настройка игнорируется без ошибки.
/// 
/// Риск: Low — все изменения обратимы, не влияют на стабильность.
/// </summary>
public sealed class GamingOptimization : IOptimization
{
    private readonly IBackupManager _backup;
    private readonly ILogger<GamingOptimization> _logger;

    private static readonly (RegistryKey BaseKey, string SubKey, string Name, int Value, string Desc)[] Changes =
    [
        // ── Game Mode ───────────────────────────────────
        // Включает Game Mode — Windows приоритизирует ресурсы для полноэкранных игр
        (Registry.CurrentUser,
         @"SOFTWARE\Microsoft\GameBar",
         "AllowAutoGameMode", 1,
         "Game Mode: автоматическая активация"),

        (Registry.CurrentUser,
         @"SOFTWARE\Microsoft\GameBar",
         "AutoGameModeEnabled", 1,
         "Game Mode: включён"),

        // ── Hardware-Accelerated GPU Scheduling ─────────
        // GPU сам планирует задачи вместо Windows — снижает input lag
        // HwSchMode: 1 = Off, 2 = On
        // Требует совместимый GPU (WDDM 2.7+):
        //   NVIDIA: GeForce 10xx (Pascal) и новее
        //   AMD: Radeon RX 5000 (RDNA) и новее
        //   Intel: Arc (Alchemist) и новее
        (Registry.LocalMachine,
         @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
         "HwSchMode", 2,
         "GPU Scheduling: аппаратное планирование"),

        // ── Отключение Game DVR (фоновая запись) ────────
        // Game DVR записывает последние N минут геймплея в фоне,
        // потребляя 5-10% GPU и увеличивая input lag
        (Registry.CurrentUser,
         @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR",
         "AppCaptureEnabled", 0,
         "Game DVR: фоновая запись отключена"),

        (Registry.CurrentUser,
         @"System\GameConfigStore",
         "GameDVR_Enabled", 0,
         "Game DVR: полностью отключён"),

        // ── Оптимизация полноэкранного режима ───────────
        // FSE (Fullscreen Exclusive) vs FSO (Fullscreen Optimizations)
        // FSEBehaviorMode = 2: предпочитать FSE (меньше input lag)
        (Registry.CurrentUser,
         @"System\GameConfigStore",
         "GameDVR_FSEBehaviorMode", 2,
         "Полноэкранный режим: предпочитать Exclusive"),

        // Уважать пользовательские настройки FSE
        (Registry.CurrentUser,
         @"System\GameConfigStore",
         "GameDVR_HonorUserFSEBehaviorMode", 1,
         "Полноэкранный режим: учитывать настройки пользователя"),

        // Совместимость DXGI с FSE
        (Registry.CurrentUser,
         @"System\GameConfigStore",
         "GameDVR_DXGIHonorFSEWindowsCompatible", 1,
         "DXGI: совместимость с полноэкранным режимом"),

        // Отключение дополнительных фич оптимизации полноэкранного режима
        (Registry.CurrentUser,
         @"System\GameConfigStore",
         "GameDVR_EFSEFeatureFlags", 0,
         "FSE Feature Flags: отключены"),
    ];

    private static readonly string[] BackupPaths =
    [
        @"HKCU\SOFTWARE\Microsoft\GameBar",
        @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR",
        @"HKCU\System\GameConfigStore",
    ];

    public OptimizationInfo Info => new(
        Id: "gaming.optimize",
        Name: "Оптимизация для игр",
        Description: "Game Mode, GPU Scheduling, отключение Game DVR и оптимизация полноэкранного режима.",
        Category: OptimizationCategory.Gaming,
        RiskLevel: RiskLevel.Low
    );

    public GamingOptimization(IBackupManager backup, ILogger<GamingOptimization> logger)
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
            // 1. Бэкап
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

            // 2. Применяем изменения
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

            _logger.LogInformation("Игровые оптимизации применены");

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
