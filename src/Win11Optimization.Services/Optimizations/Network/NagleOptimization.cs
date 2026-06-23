using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Network;
public sealed class NagleOptimization : IOptimization
{
    private const string InterfacesSubKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private const string BackupRegistryPath = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    private readonly IBackupManager _backup;
    private readonly ILogger<NagleOptimization> _logger;

    public OptimizationInfo Info => new(
        Id: "net.nagle_disable",
        Name: "Отключение Nagle's Algorithm",
        Description: "Отправляет TCP-пакеты без буферизации. Снижает задержку на 5–20 мс в играх.",
        Category: OptimizationCategory.Network,
        RiskLevel: RiskLevel.Low
    );

    public NagleOptimization(IBackupManager backup, ILogger<NagleOptimization> logger)
    {
        _backup = backup;
        _logger = logger;
    }
    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        var guids = GetActiveAdapterGuids();

        if (guids.Count == 0)
            return Task.FromResult(false);

        foreach (var guid in guids)
        {
            var subKeyPath = $@"{InterfacesSubKey}\{guid}";
            var tcpNoDelay = RegistryHelper.GetDword(Registry.LocalMachine, subKeyPath, "TCPNoDelay");
            var ackFreq = RegistryHelper.GetDword(Registry.LocalMachine, subKeyPath, "TcpAckFrequency");

            if (tcpNoDelay != 1 || ackFreq != 1)
                return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        var guids = GetActiveAdapterGuids();

        if (guids.Count == 0)
            return OptimizationResult.Failure("Не найдены активные сетевые адаптеры");
        _logger.LogInformation("Бэкап ветки реестра: {Path}", BackupRegistryPath);
        await _backup.BackupRegistryKeyAsync(Info.Id, BackupRegistryPath, ct);
        var warnings = new List<string>();
        var applied = 0;

        foreach (var guid in guids)
        {
            try
            {
                var subKeyPath = $@"{InterfacesSubKey}\{guid}";
                RegistryHelper.SetDword(Registry.LocalMachine, subKeyPath, "TCPNoDelay", 1);
                RegistryHelper.SetDword(Registry.LocalMachine, subKeyPath, "TcpAckFrequency", 1);

                applied++;
                _logger.LogInformation("Nagle отключён для адаптера: {Guid}", guid);
            }
            catch (UnauthorizedAccessException ex)
            {
                warnings.Add($"Адаптер {guid}: нет доступа ({ex.Message})");
                _logger.LogWarning(ex, "Нет доступа к ключу адаптера {Guid}", guid);
            }
            catch (Exception ex)
            {
                warnings.Add($"Адаптер {guid}: {ex.Message}");
                _logger.LogWarning(ex, "Ошибка обработки адаптера {Guid}", guid);
            }
        }

        if (applied == 0)
            return OptimizationResult.Failure(
                "Не удалось применить оптимизацию ни к одному адаптеру", warnings);

        _logger.LogInformation("Nagle отключён для {Applied}/{Total} адаптеров", applied, guids.Count);

        return warnings.Count > 0
            ? OptimizationResult.Success(warnings)
            : OptimizationResult.Success();
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

        _logger.LogInformation("Откат Nagle из бэкапа: {Id}", lastBackup.Id);
        return await _backup.RestoreRegistryKeyAsync(lastBackup.Id, ct);
    }
    private static List<string> GetActiveAdapterGuids()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                       && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => nic.Id)
            .ToList();
    }
}
