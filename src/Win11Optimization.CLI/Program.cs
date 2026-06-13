using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Win11Optimization.CLI;
using Win11Optimization.Services.DependencyInjection;

// ──────────────────────────────────────────────
//  Win11Optimization — точка входа
// ──────────────────────────────────────────────

// Проверка прав администратора (обязательно для работы с реестром)
if (!IsAdministrator())
{
    AnsiConsole.MarkupLine("[red bold][[✗]] Запустите приложение от имени администратора.[/]");
    AnsiConsole.MarkupLine("[grey]   Правый клик → «Запуск от имени администратора»[/]");
    return 1;
}

// Настройка DI-контейнера
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning); // В CLI показываем только предупреждения+
});

services.AddWin11Optimization();
services.AddTransient<App>();

using var provider = services.BuildServiceProvider();

// Запуск приложения
var app = provider.GetRequiredService<App>();
return await app.RunAsync();

// ──────────────────────────────────────────────

/// <summary>
/// Проверка прав администратора через WindowsIdentity.
/// Необходимо для работы с реестром (HKLM) и системными службами.
/// </summary>
static bool IsAdministrator()
{
    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
    var principal = new System.Security.Principal.WindowsPrincipal(identity);
    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}

// Заглушка для top-level statements
public partial class Program;
