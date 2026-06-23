using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Network;
public sealed class NetworkThrottlingOptimization : IOptimization
{
    private const string SystemProfileSubKey =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    private const string BackupRegistryPath =
        @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const int ThrottlingDisabled = unchecked((int)0xFFFFFFFF);
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
    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Бэкап: {Path}", BackupRegistryPath);
            await _backup.BackupRegistryKeyAsync(Info.Id, BackupRegistryPath, ct);
            RegistryHelper.SetDword(
                Registry.LocalMachine, SystemProfileSubKey,
                "NetworkThrottlingIndex", ThrottlingDisabled);
            _logger.LogInformation(
                "NetworkThrottlingIndex = 0x{Value:X8} (throttling отключён)",
                ThrottlingDisabled);
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
