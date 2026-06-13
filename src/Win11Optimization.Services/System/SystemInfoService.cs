using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;

namespace Win11Optimization.Services.System;

/// <summary>
/// Реализация ISystemInfo — читает информацию о текущей системе Windows.
/// 
/// Все значения кэшируются через Lazy&lt;T&gt; и вычисляются однократно при первом обращении.
/// Потокобезопасность гарантируется LazyThreadSafetyMode.ExecutionAndPublication (default).
/// 
/// Источники данных:
/// - Версия ОС: Environment.OSVersion
/// - Сборка и редакция: реестр HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion
/// - Права администратора: WindowsIdentity + WindowsPrincipal
/// </summary>
public sealed class SystemInfoService : ISystemInfo
{
    /// <summary>Ветка реестра с информацией о версии Windows.</summary>
    private const string NtVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    /// <summary>Минимальный номер сборки Windows 11 (21H2, октябрь 2021).</summary>
    private const int Windows11MinBuild = 22000;

    private readonly Lazy<string> _osVersion;
    private readonly Lazy<string> _osBuild;
    private readonly Lazy<string> _osEdition;
    private readonly Lazy<bool> _isAdmin;
    private readonly ILogger<SystemInfoService> _logger;

    public SystemInfoService(ILogger<SystemInfoService> logger)
    {
        _logger = logger;
        _osVersion = new Lazy<string>(ReadOsVersion);
        _osBuild = new Lazy<string>(ReadOsBuild);
        _osEdition = new Lazy<string>(ReadOsEdition);
        _isAdmin = new Lazy<bool>(CheckAdministrator);
    }

    /// <inheritdoc />
    public string OsVersion => _osVersion.Value;

    /// <inheritdoc />
    public string OsBuild => _osBuild.Value;

    /// <inheritdoc />
    public string OsEdition => _osEdition.Value;

    /// <inheritdoc />
    /// <remarks>
    /// Windows 11 определяется по номеру сборки >= 22000.
    /// Номер major version (10.0) одинаков для Win10 и Win11.
    /// </remarks>
    public bool IsWindows11 => int.TryParse(OsBuild, out var build) && build >= Windows11MinBuild;

    /// <inheritdoc />
    public bool IsAdministrator => _isAdmin.Value;

    /// <inheritdoc />
    public string GetSummary()
    {
        return $"""
            ОС: {OsEdition}
            Версия: {OsVersion}
            Сборка: {OsBuild}
            Windows 11: {(IsWindows11 ? "Да" : "Нет")}
            Администратор: {(IsAdministrator ? "Да" : "Нет")}
            """;
    }

    // ── Private: чтение данных ──────────────────────────────

    private string ReadOsVersion()
    {
        try
        {
            return Environment.OSVersion.Version.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать версию ОС через Environment.OSVersion");
            return "Unknown";
        }
    }

    private string ReadOsBuild()
    {
        try
        {
            // CurrentBuildNumber — строковое значение (например, "22631")
            return ReadRegistryValue("CurrentBuildNumber") ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать номер сборки из реестра");
            return "Unknown";
        }
    }

    private string ReadOsEdition()
    {
        try
        {
            // ProductName может быть "Windows 10 Pro" даже на Win11 (баг MS).
            // DisplayVersion даёт "23H2", "24H2" и т.д.
            var productName = ReadRegistryValue("ProductName") ?? "Unknown";
            var displayVersion = ReadRegistryValue("DisplayVersion");

            // Корректируем ProductName для Windows 11
            if (IsWindows11 && productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
            {
                productName = productName.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
            }

            return displayVersion is not null
                ? $"{productName} ({displayVersion})"
                : productName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать название редакции из реестра");
            return "Unknown";
        }
    }

    /// <summary>
    /// Читает строковое значение из ветки HKLM\...\CurrentVersion.
    /// Открывает ключ только для чтения — не требует прав администратора.
    /// </summary>
    private static string? ReadRegistryValue(string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(NtVersionKey);
        return key?.GetValue(valueName)?.ToString();
    }

    /// <summary>
    /// Проверка прав администратора через Windows Security API.
    /// WindowsIdentity.GetCurrent() возвращает токен текущего процесса.
    /// </summary>
    private static bool CheckAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
