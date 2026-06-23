using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Win11Optimization.Core.Interfaces;

namespace Win11Optimization.Services.System;
public sealed class SystemInfoService : ISystemInfo
{
    private const string NtVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
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
    public string OsVersion => _osVersion.Value;
    public string OsBuild => _osBuild.Value;
    public string OsEdition => _osEdition.Value;
    public bool IsWindows11 => int.TryParse(OsBuild, out var build) && build >= Windows11MinBuild;
    public bool IsAdministrator => _isAdmin.Value;
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
            var productName = ReadRegistryValue("ProductName") ?? "Unknown";
            var displayVersion = ReadRegistryValue("DisplayVersion");
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
    private static string? ReadRegistryValue(string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(NtVersionKey);
        return key?.GetValue(valueName)?.ToString();
    }
    private static bool CheckAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
