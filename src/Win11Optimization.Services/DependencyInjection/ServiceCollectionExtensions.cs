using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Services.Backup;
using Win11Optimization.Services.System;

namespace Win11Optimization.Services.DependencyInjection;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWin11Optimization(this IServiceCollection services)
    {
        services.AddSingleton<ISystemInfo, SystemInfoService>();
        services.AddSingleton<IBackupManager, BackupManagerService>();
        services.AddSingleton<IStateStorage, StateStorageService>();
        var optimizationType = typeof(IOptimization);
        var optimizationTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => optimizationType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in optimizationTypes)
        {
            services.AddTransient(optimizationType, type);
        }

        return services;
    }
}
