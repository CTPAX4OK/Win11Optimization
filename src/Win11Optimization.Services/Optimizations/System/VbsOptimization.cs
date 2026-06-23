using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Core.Localization;
using Win11Optimization.Services.Helpers;
using System.Threading;
using System.Threading.Tasks;

namespace Win11Optimization.Services.Optimizations.System;

public sealed class VbsOptimization : IOptimization
{
    private const string DeviceGuardKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string HypervisorKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
    
    private readonly IBackupManager _backup;

    public VbsOptimization(IBackupManager backup)
    {
        _backup = backup;
    }

    public OptimizationInfo Info => new OptimizationInfo(
        "vbs_disable",
        Strings.IsRussian ? "Отключение VBS и Memory Integrity" : "Disable VBS and Memory Integrity",
        Strings.IsRussian 
            ? "Отключает Изоляцию ядра и VBS. Существенно повышает FPS на AMD Ryzen, но снижает безопасность." 
            : "Disables Core Isolation and VBS. Substantially increases FPS on AMD Ryzen but lowers security.",
        OptimizationCategory.System,
        RiskLevel.High
    );

    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        var vbsEnabled = (RegistryHelper.GetDword(Microsoft.Win32.Registry.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity") ?? 1) == 0;
        var hvciEnabled = (RegistryHelper.GetDword(Microsoft.Win32.Registry.LocalMachine, HypervisorKey, "Enabled") ?? 1) == 0;
        return Task.FromResult(vbsEnabled && hvciEnabled);
    }

    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        await _backup.BackupRegistryKeyAsync(Info.Id, @"HKEY_LOCAL_MACHINE\" + DeviceGuardKey, ct);

        RegistryHelper.SetDword(Microsoft.Win32.Registry.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity", 0);
        RegistryHelper.SetDword(Microsoft.Win32.Registry.LocalMachine, HypervisorKey, "Enabled", 0);

        return OptimizationResult.Success();
    }

    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        return await _backup.RestoreRegistryKeyAsync(Info.Id, ct);
    }
}
