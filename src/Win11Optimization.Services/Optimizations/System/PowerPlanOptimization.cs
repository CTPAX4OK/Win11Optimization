using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Core.Localization;
using Win11Optimization.Services.Helpers;
using Microsoft.Win32;

namespace Win11Optimization.Services.Optimizations.System;

public sealed class PowerPlanOptimization : IOptimization
{
    private readonly ILogger<PowerPlanOptimization> _logger;
    private readonly IStateStorage _stateStorage;
    private const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string StateKey = "PreviousPowerPlanGuid";

    public OptimizationInfo Info => new OptimizationInfo(
        "ultimate_power_plan",
        Strings.IsRussian ? "Ultimate Power Plan" : "Ultimate Power Plan",
        Strings.IsRussian 
            ? "Разблокирует и включает 'Максимальную производительность', отключает парковку ядер и энергосбережение USB."
            : "Unlocks and enables 'Ultimate Performance' plan, disables core parking and USB selective suspend.",
        OptimizationCategory.System,
        RiskLevel.Low
    );

    public PowerPlanOptimization(ILogger<PowerPlanOptimization> logger, IStateStorage stateStorage)
    {
        _logger = logger;
        _stateStorage = stateStorage;
    }

    public async Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        var (success, output, _) = await RunPowercfgAsync("/getactivescheme", ct);
        if (!success) return false;

        return output.Contains(UltimatePerformanceGuid, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        if (await IsAppliedAsync(ct)) return OptimizationResult.Success();
        var (s, activeOut, _) = await RunPowercfgAsync("/getactivescheme", ct);
        if (s)
        {
            var match = Regex.Match(activeOut, @"([0-9a-f]{8}-([0-9a-f]{4}-){3}[0-9a-f]{12})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var guid = match.Groups[1].Value;
                await _stateStorage.SetStateAsync(Info.Id, StateKey, guid, ct);
            }
        }
        var (listS, listOut, _) = await RunPowercfgAsync("/list", ct);
        if (listS && !listOut.Contains(UltimatePerformanceGuid, StringComparison.OrdinalIgnoreCase))
        {
            await RunPowercfgAsync($"-duplicatescheme {UltimatePerformanceGuid}", ct);
        }
        var (setS, _, error) = await RunPowercfgAsync($"/setactive {UltimatePerformanceGuid}", ct);
        if (!setS)
            return OptimizationResult.Failure($"powercfg error: {error}");
        await RunPowercfgAsync($"/setacvalueindex {UltimatePerformanceGuid} 2a737441-1930-4402-8d77-b2bea1222653 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0", ct);
        await RunPowercfgAsync($"/setdcvalueindex {UltimatePerformanceGuid} 2a737441-1930-4402-8d77-b2bea1222653 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0", ct);
        RegistryHelper.SetDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583", "Attributes", 0);
        await RunPowercfgAsync($"/setacvalueindex {UltimatePerformanceGuid} SUB_PROCESSOR CPMINCORES 100", ct);
        await RunPowercfgAsync($"/setactive {UltimatePerformanceGuid}", ct);

        return OptimizationResult.Success();
    }

    public async Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        var prevGuid = await _stateStorage.GetStateAsync<string>(Info.Id, StateKey, ct);
        if (string.IsNullOrEmpty(prevGuid))
        {
            prevGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        }

        await RunPowercfgAsync($"/setactive {prevGuid}", ct);
        await _stateStorage.ClearStateAsync(Info.Id, StateKey, ct);

        return OptimizationResult.Success();
    }

    private static async Task<(bool Success, string Output, string Error)> RunPowercfgAsync(
        string args, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode == 0, stdout, stderr);
    }
}
