using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Services.Helpers;

namespace Win11Optimization.Services.Optimizations.Network;

/// <summary>
/// Комплексная оптимизация TCP/IP стека Windows 11.
/// 
/// Два уровня изменений:
/// 
/// 1. РЕЕСТР (HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters):
///    - TcpTimedWaitDelay = 32  → сокеты в TIME_WAIT освобождаются за 32 сек (вместо 120)
///    - DefaultTTL = 64         → оптимальный TTL для игровых серверов
///    - MaxUserPort = 65534     → полный диапазон эфемерных портов
/// 
/// 2. NETSH (TCP Global Parameters):
///    - autotuninglevel=normal  → динамическая подстройка окна приёма
///    - timestamps=disabled     → убирает 12 байт overhead из каждого пакета
///    - ecncapability=disabled  → совместимость с игровыми серверами/роутерами
///    - rsc=disabled            → отключает аппаратную коалесценцию (снижает input lag)
/// 
/// Откат: реестр — из бэкапа, netsh — возврат к Windows defaults.
/// </summary>
public sealed class TcpOptimization : IOptimization
{
    private const string TcpParamsSubKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
    private const string BackupRegistryPath = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";

    private readonly IBackupManager _backup;
    private readonly ILogger<TcpOptimization> _logger;

    /// <summary>
    /// Registry-параметры для оптимизации TCP/IP.
    /// Формат: (имя значения, оптимальное значение, описание для лога).
    /// </summary>
    private static readonly (string Name, int Value, string Description)[] RegistryParams =
    [
        ("TcpTimedWaitDelay", 32,
            "TIME_WAIT = 32 сек (быстрое освобождение портов при переподключении)"),

        ("DefaultTTL", 64,
            "TTL = 64 (стандарт Linux/macOS, оптимально для игровых серверов)"),

        ("MaxUserPort", 65534,
            "Макс. эфемерный порт = 65534 (полный диапазон для исходящих соединений)")
    ];

    /// <summary>
    /// netsh-команды для оптимизации TCP стека.
    /// Формат: (аргументы apply, аргументы rollback, описание).
    /// 
    /// Rollback использует Windows defaults, а не сохранённые значения,
    /// потому что вывод `netsh show` зависит от локали (RU/EN).
    /// </summary>
    private static readonly (string ApplyArgs, string RollbackArgs, string Description)[] NetshCommands =
    [
        ("int tcp set global autotuninglevel=normal",
         "int tcp set global autotuninglevel=normal",
         "TCP Auto-Tuning: normal"),

        ("int tcp set global timestamps=disabled",
         "int tcp set global timestamps=default",
         "TCP Timestamps: отключены (−12 байт overhead на пакет)"),

        ("int tcp set global ecncapability=disabled",
         "int tcp set global ecncapability=default",
         "ECN: отключён (совместимость с игровыми серверами)"),

        ("int tcp set global rsc=disabled",
         "int tcp set global rsc=enabled",
         "RSC: отключён (снижает задержку обработки входящих пакетов)")
    ];

    public OptimizationInfo Info => new(
        Id: "net.tcp_optimize",
        Name: "Оптимизация TCP/IP стека",
        Description: "Настройка таймаутов, TTL, портов и TCP-параметров для снижения задержки и джиттера.",
        Category: OptimizationCategory.Network,
        RiskLevel: RiskLevel.Low
    );

    public TcpOptimization(IBackupManager backup, ILogger<TcpOptimization> logger)
    {
        _backup = backup;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Проверяет только registry-параметры (они — основной индикатор).
    /// netsh-состояние не проверяется: парсинг вывода зависит от локали.
    /// </remarks>
    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        foreach (var (name, expectedValue, _) in RegistryParams)
        {
            var current = RegistryHelper.GetDword(Registry.LocalMachine, TcpParamsSubKey, name);
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
            // ── 1. Бэкап реестра ──────────────────────────
            _logger.LogInformation("Бэкап: {Path}", BackupRegistryPath);
            await _backup.BackupRegistryKeyAsync(Info.Id, BackupRegistryPath, ct);

            // ── 2. Registry: TCP параметры ────────────────
            foreach (var (name, value, desc) in RegistryParams)
            {
                var oldValue = RegistryHelper.GetDword(Registry.LocalMachine, TcpParamsSubKey, name);
                RegistryHelper.SetDword(Registry.LocalMachine, TcpParamsSubKey, name, value);
                _logger.LogInformation(
                    "{Name}: {Old} → {New} ({Desc})",
                    name, oldValue?.ToString() ?? "null", value, desc);
            }

            // ── 3. netsh: TCP Global Parameters ───────────
            foreach (var (applyArgs, _, desc) in NetshCommands)
            {
                var (success, error) = await RunNetshAsync(applyArgs, ct);
                if (!success)
                {
                    // netsh ошибки не фатальны — добавляем как warning
                    warnings.Add($"{desc}: {error}");
                    _logger.LogWarning("netsh не применён: {Args} → {Error}", applyArgs, error);
                }
                else
                {
                    _logger.LogInformation("netsh: {Desc}", desc);
                }
            }

            _logger.LogInformation("TCP/IP стек оптимизирован");

            return warnings.Count > 0
                ? OptimizationResult.Success(warnings)
                : OptimizationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка оптимизации TCP/IP стека");
            return OptimizationResult.Failure($"Ошибка: {ex.Message}", warnings);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Двухэтапный откат:
    /// 1. Реестр — восстановление из .reg бэкапа (точное)
    /// 2. netsh — возврат к Windows defaults (приблизительное)
    /// 
    /// Если бэкап реестра отсутствует, netsh всё равно откатывается.
    /// </remarks>
    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // ── 1. Откат реестра ──────────────────────────────
        var backups = await _backup.ListBackupsAsync(ct);
        var lastBackup = backups
            .Where(b => b.OptimizationId == Info.Id && b.Type == BackupType.RegistryKey)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefault();

        if (lastBackup is not null)
        {
            var regResult = await _backup.RestoreRegistryKeyAsync(lastBackup.Id, ct);
            if (!regResult.IsSuccess)
            {
                warnings.Add($"Реестр: {regResult.ErrorMessage}");
                _logger.LogWarning("Откат реестра не удался: {Error}", regResult.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("Реестр восстановлен из бэкапа: {Id}", lastBackup.Id);
            }
        }
        else
        {
            warnings.Add("Бэкап реестра не найден — параметры реестра не восстановлены");
            _logger.LogWarning("Бэкап реестра не найден для {Id}", Info.Id);
        }

        // ── 2. Откат netsh к defaults ─────────────────────
        foreach (var (_, rollbackArgs, desc) in NetshCommands)
        {
            var (success, error) = await RunNetshAsync(rollbackArgs, ct);
            if (!success)
            {
                warnings.Add($"netsh {desc}: {error}");
                _logger.LogWarning("netsh rollback failed: {Args} → {Error}", rollbackArgs, error);
            }
            else
            {
                _logger.LogInformation("netsh откат: {Desc}", desc);
            }
        }

        return warnings.Count > 0
            ? OptimizationResult.Success(warnings)
            : OptimizationResult.Success();
    }

    // ── netsh helper ──────────────────────────────────────

    /// <summary>
    /// Выполняет команду netsh.exe и возвращает результат.
    /// 
    /// netsh — встроенная утилита Windows для настройки сетевого стека.
    /// CreateNoWindow = true — не открывает окно консоли.
    /// </summary>
    private static async Task<(bool Success, string Error)> RunNetshAsync(
        string args, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "netsh.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        // Читаем оба потока до WaitForExit, чтобы избежать
        // deadlock на буфере (MSDN рекомендация)
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var errorMsg = string.IsNullOrWhiteSpace(stderr)
                ? stdout.Trim()
                : stderr.Trim();
            return (false, errorMsg);
        }

        return (true, string.Empty);
    }
}
