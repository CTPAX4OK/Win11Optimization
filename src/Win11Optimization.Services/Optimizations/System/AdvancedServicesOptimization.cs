using System.Threading;
using System.Threading.Tasks;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Core.Localization;
using Win11Optimization.Services.Helpers;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Win11Optimization.Services.Optimizations.System;

public sealed class AdvancedServicesOptimization : IOptimization
{
    private readonly IBackupManager _backup;
    private readonly IStateStorage _stateStorage;

    private readonly string[] _servicesToDisable = 
    {
        "WSearch", "Spooler", "WerSvc", 
        "XboxGipSvc", "xbgm", "XblAuthManager", "XblGameSave", "XboxNetApiSvc"
    };

    public AdvancedServicesOptimization(IBackupManager backup, IStateStorage stateStorage)
    {
        _backup = backup;
        _stateStorage = stateStorage;
    }

    public OptimizationInfo Info => new OptimizationInfo(
        "adv_services",
        Strings.IsRussian ? "Агрессивное отключение служб" : "Aggressive Services Disabling",
        Strings.IsRussian 
            ? "Отключает Поиск Windows, Диспетчер печати, Отчеты об ошибках и службы Xbox для снижения фоновой нагрузки."
            : "Disables Windows Search, Print Spooler, Error Reporting, and Xbox services to reduce background load.",
        OptimizationCategory.System,
        RiskLevel.Medium
    );

    public async Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        foreach (var svc in _servicesToDisable)
        {
            var startType = RegistryHelper.GetDword(Registry.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{svc}", "Start");
            if (startType is not null && startType != 4)
            {
                return false;
            }
        }
        return true;
    }

    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        foreach (var svc in _servicesToDisable)
        {
            var keyPath = $@"SYSTEM\CurrentControlSet\Services\{svc}";
            var currentStart = RegistryHelper.GetDword(Registry.LocalMachine, keyPath, "Start");
            if (currentStart is not null)
            {
                await _stateStorage.SetStateAsync(Info.Id, svc, currentStart.Value, ct);
                await _backup.BackupRegistryKeyAsync($"{Info.Id}_{svc}", @"HKEY_LOCAL_MACHINE\" + keyPath, ct);
                RegistryHelper.SetDword(Registry.LocalMachine, keyPath, "Start", 4);
            }
        }

        return OptimizationResult.Success();
    }

    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        var errors = new List<string>();
        foreach (var svc in _servicesToDisable)
        {
            var keyPath = $@"SYSTEM\CurrentControlSet\Services\{svc}";
            var oldStart = await _stateStorage.GetStateAsync<int?>(Info.Id, svc, ct);
            
            if (oldStart.HasValue)
            {
                RegistryHelper.SetDword(Registry.LocalMachine, keyPath, "Start", oldStart.Value);
            }
            else
            {
                var res = await _backup.RestoreRegistryKeyAsync($"{Info.Id}_{svc}", ct);
                if (!res.IsSuccess) errors.Add(res.ErrorMessage ?? $"Error restoring {svc}");
            }
        }

        if (errors.Count > 0) return OptimizationResult.Failure(string.Join("; ", errors));
        
        await _stateStorage.ClearStateAsync(Info.Id, "WSearch", ct);
        return OptimizationResult.Success();
    }
}
