using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Network;

/// <summary>
/// Отключение Network Throttling (MMCSS) и оптимизация SystemResponsiveness.
/// 
/// Windows по умолчанию ограничивает сетевой трафик при воспроизведении
/// мультимедиа через Multimedia Class Scheduler Service (MMCSS).
/// Ограничение: 10 пакетов в миллисекунду (NetworkThrottlingIndex = 10).
/// 
/// Для игр это нежелательно — убираем потолок полностью.
/// 
/// Что делает:
/// - NetworkThrottlingIndex = 0xFFFFFFFF → без ограничения пропускной способности
/// - SystemResponsiveness = 0 → все ресурсы CPU отдаются foreground-задачам
/// 
/// Реестр: HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile
/// </summary>
public sealed class NetworkThrottlingOptimization : IOptimization
{
    private const string SystemProfileSubKey =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    private const string BackupRegistryPath =
        @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    /// <summary>
    /// 0xFFFFFFFF (-1 в signed int) — полностью отключает throttling.
    /// Значение по умолчанию в Windows: 10 (10 пакетов/мс).
    /// </summary>
    private const int ThrottlingDisabled = unchecked((int)0xFFFFFFFF);

    /// <summary>
    /// 0 = максимальный приоритет foreground-задачам.
    /// Значение по умолчанию в Windows: 20 (20% CPU резервируется для фоновых задач).
    /// 0 = все ресурсы CPU отдаются активному приложению (игре).
    /// </summary>
    private const int SystemResponsivenessGaming = 0;

    private readonly IBackupManager _backup;
    private readonly ILogger<NetworkThrottlingOptimization> _logger;

    public OptimizationInfo Info => new(
        Id: "net.throttling_disable",
        Name: "Отключение Network Throttling",
        Description: "Снимает ограничение пропускной способности сети (MMCSS) и отдаёт CPU играм.",
        Category: OptimizationCategory.Network,
        RiskLevel: RiskLevel.Low
    );

    public NetworkThrottlingOptimization(
        IBackupManager backup,
        ILogger<NetworkThrottlingOptimization> logger)
    {
        _backup = backup;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        var throttling = RegistryHelper.GetDword(
            Registry.LocalMachine, SystemProfileSubKey, "NetworkThrottlingIndex");
        var responsiveness = RegistryHelper.GetDword(
            Registry.LocalMachine, SystemProfileSubKey, "SystemResponsiveness");

        var isApplied = throttling == ThrottlingDisabled
                     && responsiveness == SystemResponsivenessGaming;

        return Task.FromResult(isApplied);
    }

    /// <inheritdoc />
    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        try
        {
            // 1. Бэкап
            _logger.LogInformation("Бэкап: {Path}", BackupRegistryPath);
            await _backup.BackupRegistryKeyAsync(Info.Id, BackupRegistryPath, ct);

            // 2. Отключаем Network Throttling
            RegistryHelper.SetDword(
                Registry.LocalMachine, SystemProfileSubKey,
                "NetworkThrottlingIndex", ThrottlingDisabled);
            _logger.LogInformation(
                "NetworkThrottlingIndex = 0x{Value:X8} (throttling отключён)",
                ThrottlingDisabled);

            // 3. CPU → foreground (игры)
            RegistryHelper.SetDword(
                Registry.LocalMachine, SystemProfileSubKey,
                "SystemResponsiveness", SystemResponsivenessGaming);
            _logger.LogInformation(
                "SystemResponsiveness = {Value} (все ресурсы — foreground)",
                SystemResponsivenessGaming);

            return OptimizationResult.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Нет доступа к ветке SystemProfile");
            return OptimizationResult.Failure($"Нет доступа к реестру: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отключения Network Throttling");
            return OptimizationResult.Failure($"Ошибка: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        var backups = await _backup.ListBackupsAsync(ct);
        var lastBackup = backups
            .Where(b => b.OptimizationId == Info.Id && b.Type == BackupType.RegistryKey)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefault();

        if (lastBackup is null)
            return OptimizationResult.Failure(
                "Бэкап не найден. Откат невозможен. Используйте точку восстановления системы.");

        _logger.LogInformation("Откат Network Throttling из бэкапа: {Id}", lastBackup.Id);
        return await _backup.RestoreRegistryKeyAsync(lastBackup.Id, ct);
    }
}
