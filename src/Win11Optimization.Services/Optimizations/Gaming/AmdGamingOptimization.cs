using Microsoft.Win32;
using System.Threading;
using System.Threading.Tasks;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Core.Localization;
using Win11Optimization.Services.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace Win11Optimization.Services.Optimizations.Gaming;

public sealed class AmdGamingOptimization : IOptimization
{
    private const string ControlClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private readonly IBackupManager _backup;

    public AmdGamingOptimization(IBackupManager backup)
    {
        _backup = backup;
    }

    public OptimizationInfo Info => new OptimizationInfo(
        "amd_gaming",
        Strings.IsRussian ? "AMD Gaming (ULPS)" : "AMD Gaming (ULPS)",
        Strings.IsRussian 
            ? "Отключает Ultra Low Power State (ULPS) для видеокарт AMD Radeon, что убирает микрофризы и статтеры в играх."
            : "Disables Ultra Low Power State (ULPS) for AMD Radeon GPUs, eliminating micro-stutters in games.",
        OptimizationCategory.Gaming,
        RiskLevel.Low
    );

    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        var keys = GetAmdAdapterKeys();
        if (keys.Count == 0) return Task.FromResult(true);

        foreach (var key in keys)
        {
            var enableUlps = RegistryHelper.GetDword(Registry.LocalMachine, key, "EnableUlps");
            if (enableUlps is not null && enableUlps != 0)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        var keys = GetAmdAdapterKeys();
        if (keys.Count == 0) return OptimizationResult.Success();

        foreach (var key in keys)
        {
            await _backup.BackupRegistryKeyAsync($"{Info.Id}_{key.Replace(@"\", "_")}", $@"HKEY_LOCAL_MACHINE\{key}", ct);
            RegistryHelper.SetDword(Registry.LocalMachine, key, "EnableUlps", 0);
        }

        return OptimizationResult.Success();
    }

    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        var keys = GetAmdAdapterKeys();
        if (keys.Count == 0) return OptimizationResult.Success();

        var errors = new List<string>();
        foreach (var key in keys)
        {
            var res = await _backup.RestoreRegistryKeyAsync($"{Info.Id}_{key.Replace(@"\", "_")}", ct);
            if (!res.IsSuccess) errors.Add(res.ErrorMessage ?? "Unknown Error");
        }

        if (errors.Count > 0) return OptimizationResult.Failure(string.Join("; ", errors));
        return OptimizationResult.Success();
    }

    private List<string> GetAmdAdapterKeys()
    {
        var result = new List<string>();
        using var baseClassKey = Registry.LocalMachine.OpenSubKey(ControlClassKey);
        if (baseClassKey == null) return result;

        foreach (var subKeyName in baseClassKey.GetSubKeyNames())
        {
            RegistryKey? subKey = null;
            try
            {
                subKey = baseClassKey.OpenSubKey(subKeyName, false);
            }
            catch (global::System.Security.SecurityException)
            {
                continue;
            }

            if (subKey == null) continue;

            using (subKey)
            {
                var providerName = subKey.GetValue("ProviderName")?.ToString();
                if (providerName != null && providerName.Contains("Advanced Micro Devices"))
                {
                    var enableUlps = subKey.GetValue("EnableUlps");
                    if (enableUlps != null)
                    {
                        result.Add($@"{ControlClassKey}\{subKeyName}");
                    }
                }
            }
        }

        return result;
    }
}
