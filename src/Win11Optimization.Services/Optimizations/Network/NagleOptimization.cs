using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Network;

/// <summary>
/// Отключение Nagle's Algorithm для всех активных сетевых адаптеров.
/// 
/// Nagle's Algorithm буферизует мелкие TCP-пакеты и отправляет их пачкой,
/// что экономит bandwidth, но добавляет задержку (до 200 мс по RFC, обычно 5–20 мс).
/// Для игр, где каждый тик отправляет маленький пакет (64–256 байт), это критично.
/// 
/// Что делает:
/// - TCPNoDelay = 1 → отключает буферизацию (отправка немедленно)
/// - TcpAckFrequency = 1 → подтверждение каждого пакета (вместо отложенного ACK)
/// 
/// Изменения применяются PER-ADAPTER через реестр:
/// HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{GUID}
/// </summary>
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

    /// <inheritdoc />
    /// <remarks>
    /// Проверяет, что TCPNoDelay=1 и TcpAckFrequency=1 установлены
    /// для ВСЕХ активных сетевых адаптеров. Если хотя бы у одного
    /// адаптера значения не установлены — возвращает false.
    /// </remarks>
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

    /// <inheritdoc />
    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        var guids = GetActiveAdapterGuids();

        if (guids.Count == 0)
            return OptimizationResult.Failure("Не найдены активные сетевые адаптеры");

        // 1. Бэкап всей ветки Interfaces (включает все адаптеры)
        _logger.LogInformation("Бэкап ветки реестра: {Path}", BackupRegistryPath);
        await _backup.BackupRegistryKeyAsync(Info.Id, BackupRegistryPath, ct);

        // 2. Применяем к каждому активному адаптеру
        var warnings = new List<string>();
        var applied = 0;

        foreach (var guid in guids)
        {
            try
            {
                var subKeyPath = $@"{InterfacesSubKey}\{guid}";

                // TCPNoDelay = 1: отключает алгоритм Нейгла
                RegistryHelper.SetDword(Registry.LocalMachine, subKeyPath, "TCPNoDelay", 1);

                // TcpAckFrequency = 1: немедленное подтверждение (вместо delayed ACK)
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

    /// <inheritdoc />
    /// <remarks>
    /// Восстанавливает всю ветку Interfaces из последнего бэкапа.
    /// Бэкап ищется по optimizationId = "net.nagle_disable".
    /// </remarks>
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

    // ── Обнаружение адаптеров ──────────────────────────────

    /// <summary>
    /// Возвращает GUID'ы активных сетевых адаптеров через System.Net.NetworkInformation.
    /// Исключает Loopback (127.0.0.1) — он не участвует в сетевых играх.
    /// 
    /// GUID адаптера совпадает с именем подключа в реестре:
    /// HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{GUID}
    /// </summary>
    private static List<string> GetActiveAdapterGuids()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                       && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => nic.Id)
            .ToList();
    }
}
