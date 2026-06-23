using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Core.Localization;
using Win11Optimization.Services.Helpers;
using System.Threading;
using System.Threading.Tasks;

namespace Win11Optimization.Services.Optimizations.System;

public sealed class ProcessOptimization : IOptimization
{
    private const string PriorityKey = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
    private const string MemoryKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

    private readonly IBackupManager _backup;

    public ProcessOptimization(IBackupManager backup)
    {
        _backup = backup;
    }

    public OptimizationInfo Info => new OptimizationInfo(
        "process_opt",
        Strings.IsRussian ? "Оптимизация Процессов и Памяти" : "Process and Memory Optimization",
        Strings.IsRussian 
            ? "Фокусирует ресурсы ЦП на активном окне (Win32PrioritySeparation = 26 Hex) и оптимизирует распределение системной памяти."
            : "Focuses CPU resources on the active window (Win32PrioritySeparation = 26 Hex) and optimizes system memory allocation.",
        OptimizationCategory.System,
        RiskLevel.Low
    );

    public Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        var priority = RegistryHelper.GetDword(Microsoft.Win32.Registry.LocalMachine, PriorityKey, "Win32PrioritySeparation") == 0x26;
        var largeCache = RegistryHelper.GetDword(Microsoft.Win32.Registry.LocalMachine, MemoryKey, "LargeSystemCache") == 0;
        
        return Task.FromResult(priority && largeCache);
    }

    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        await _backup.BackupRegistryKeyAsync($"{Info.Id}_priority", @"HKEY_LOCAL_MACHINE\" + PriorityKey, ct);
        await _backup.BackupRegistryKeyAsync($"{Info.Id}_memory", @"HKEY_LOCAL_MACHINE\" + MemoryKey, ct);

        RegistryHelper.SetDword(Microsoft.Win32.Registry.LocalMachine, PriorityKey, "Win32PrioritySeparation", 0x26);
        RegistryHelper.SetDword(Microsoft.Win32.Registry.LocalMachine, MemoryKey, "LargeSystemCache", 0);

        return OptimizationResult.Success();
    }

    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        var res1 = await _backup.RestoreRegistryKeyAsync($"{Info.Id}_priority", ct);
        var res2 = await _backup.RestoreRegistryKeyAsync($"{Info.Id}_memory", ct);

        if (!res1.IsSuccess || !res2.IsSuccess)
            return OptimizationResult.Failure("Failed to restore one or more registry keys.");

        return OptimizationResult.Success();
    }
}
