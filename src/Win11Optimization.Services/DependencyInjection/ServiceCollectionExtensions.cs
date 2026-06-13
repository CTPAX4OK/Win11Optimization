using Microsoft.Extensions.DependencyInjection;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Services.Backup;
using Win11Optimization.Services.Optimizations.Gaming;
using Win11Optimization.Services.Optimizations.Network;
using Win11Optimization.Services.Optimizations.Privacy;
using Win11Optimization.Services.System;

namespace Win11Optimization.Services.DependencyInjection;

/// <summary>
/// Регистрация всех сервисов Win11Optimization в DI-контейнере.
/// 
/// Порядок:
/// 1. Инфраструктура (Singleton)
/// 2. Оптимизации по категориям (Transient → IEnumerable&lt;IOptimization&gt;)
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWin11Optimization(this IServiceCollection services)
    {
        // ── Инфраструктура ──────────────────────────────
        services.AddSingleton<ISystemInfo, SystemInfoService>();
        services.AddSingleton<IBackupManager, BackupManagerService>();

        // ── Сеть ────────────────────────────────────────
        services.AddTransient<IOptimization, NagleOptimization>();
        services.AddTransient<IOptimization, NetworkThrottlingOptimization>();
        services.AddTransient<IOptimization, TcpOptimization>();

        // ── Приватность ─────────────────────────────────
        services.AddTransient<IOptimization, TelemetryOptimization>();
        services.AddTransient<IOptimization, PrivacyTweaksOptimization>();

        // ── Игры ────────────────────────────────────────
        services.AddTransient<IOptimization, GamingOptimization>();

        // ── Система ───────────────────────────────
        services.AddTransient<IOptimization, Win11Optimization.Services.Optimizations.System.ServicesOptimization>();
        services.AddTransient<IOptimization, Win11Optimization.Services.Optimizations.System.VisualEffectsOptimization>();
        services.AddTransient<IOptimization, Win11Optimization.Services.Optimizations.System.PowerPlanOptimization>();

        return services;
    }
}
