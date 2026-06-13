using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;

namespace Win11Optimization.Services.Optimizations.System;

/// <summary>
/// Оптимизация схемы электропитания (Power Plan).
/// Устанавливает схему "Высокая производительность" (High Performance) для 
/// отключения агрессивного энергосбережения CPU, парковки ядер и т.д.
/// 
/// Механизм: использует утилиту командной строки `powercfg.exe`.
/// 
/// Риск: Low (может увеличить энергопотребление на ноутбуках).
/// </summary>
public sealed class PowerPlanOptimization : IOptimization
{
    private readonly ILogger<PowerPlanOptimization> _logger;
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public OptimizationInfo Info => new(
        Id: "system.power_plan",
        Name: "Схема электропитания",
        Description: "Включает схему 'Высокая производительность' (High Performance).",
        Category: OptimizationCategory.System,
        RiskLevel: RiskLevel.Low
    );

    // Поскольку powercfg не поддерживает бэкапы через реестр, 
    // сохранять оригинальную схему между перезапусками сложно без отдельного файла конфигурации.
    // Пока реализуем только применение. В рамках реального приложения можно сохранять стейт в JSON.

    public PowerPlanOptimization(ILogger<PowerPlanOptimization> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsAppliedAsync(CancellationToken ct = default)
    {
        var (success, output, _) = await RunPowercfgAsync("/getactivescheme", ct);
        if (!success) return false;

        return output.Contains(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<OptimizationResult> ApplyAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // Проверяем, активна ли уже схема
        if (await IsAppliedAsync(ct))
        {
            return OptimizationResult.Success();
        }

        var (success, _, error) = await RunPowercfgAsync($"/setactive {HighPerformanceGuid}", ct);

        if (!success)
        {
            _logger.LogError("Не удалось установить схему электропитания: {Error}", error);
            return OptimizationResult.Failure($"Ошибка powercfg: {error}", warnings);
        }

        _logger.LogInformation("Схема электропитания изменена на 'Высокая производительность'.");
        return OptimizationResult.Success();
    }

    public Task<OptimizationResult> RollbackAsync(CancellationToken ct = default)
    {
        // Для полноценного отката необходимо сохранять GUID предыдущей схемы.
        // Здесь для упрощения мы возвращаем сбалансированную схему (Windows Default).
        const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        
        RunPowercfgAsync($"/setactive {BalancedGuid}", ct).GetAwaiter().GetResult();
        
        _logger.LogInformation("Схема электропитания сброшена на 'Сбалансированная'.");
        return Task.FromResult(OptimizationResult.Success(new[] { "Установлена стандартная 'Сбалансированная' схема." }));
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
