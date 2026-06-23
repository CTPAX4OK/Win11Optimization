using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Win11Optimization.CLI;
using Win11Optimization.Services.DependencyInjection;
if (!IsAdministrator())
{
    AnsiConsole.MarkupLine("[red bold][[✗]] Запустите приложение от имени администратора.[/]");
    AnsiConsole.MarkupLine("[grey]   Правый клик → «Запуск от имени администратора»[/]");
    return 1;
}
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});

services.AddWin11Optimization();
services.AddTransient<App>();

using var provider = services.BuildServiceProvider();
var app = provider.GetRequiredService<App>();
return await app.RunAsync();
static bool IsAdministrator()
{
    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
    var principal = new System.Security.Principal.WindowsPrincipal(identity);
    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}
public partial class Program;
