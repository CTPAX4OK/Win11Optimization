using Microsoft.Extensions.Logging;
using Spectre.Console;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;

namespace Win11Optimization.CLI;

/// <summary>
/// Главный класс приложения — интерактивное CLI-меню.
/// Получает зависимости через DI и управляет циклом взаимодействия.
/// </summary>
public sealed class App
{
    private readonly IReadOnlyList<IOptimization> _optimizations;
    private readonly IBackupManager _backup;
    private readonly ISystemInfo _sysInfo;
    private readonly ILogger<App> _logger;

    public App(
        IEnumerable<IOptimization> optimizations,
        IBackupManager backup,
        ISystemInfo sysInfo,
        ILogger<App> logger)
    {
        _optimizations = optimizations.ToList();
        _backup = backup;
        _sysInfo = sysInfo;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════
    //  Точка входа
    // ═══════════════════════════════════════════════════════

    public async Task<int> RunAsync()
    {
        ShowBanner();
        ShowSystemInfo();

        AnsiConsole.MarkupLine($"[green][[✓]][/] Модули оптимизации: [cyan]{_optimizations.Count}[/]");
        AnsiConsole.WriteLine();

        // Главный цикл
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]═══ Главное меню ═══[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(
                        "📊  Статус оптимизаций",
                        "🚀  Применить оптимизации",
                        "↩️   Откатить оптимизации",
                        "💾  Создать точку восстановления",
                        "📋  Управление бэкапами",
                        "❌  Выход"));

            AnsiConsole.WriteLine();

            switch (choice)
            {
                case "📊  Статус оптимизаций":
                    await ShowStatusAsync();
                    break;
                case "🚀  Применить оптимизации":
                    await ApplyOptimizationsAsync();
                    break;
                case "↩️   Откатить оптимизации":
                    await RollbackOptimizationsAsync();
                    break;
                case "💾  Создать точку восстановления":
                    await CreateRestorePointAsync();
                    break;
                case "📋  Управление бэкапами":
                    await ShowBackupsAsync();
                    break;
                case "❌  Выход":
                    AnsiConsole.MarkupLine("[grey]До свидания![/]");
                    return 0;
            }

            AnsiConsole.WriteLine();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Баннер и системная информация
    // ═══════════════════════════════════════════════════════

    private void ShowBanner()
    {
        AnsiConsole.Write(
            new FigletText("Win11 Optimizer")
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[grey]v0.1.0 | Модульный оптимизатор Windows 11 | MIT License[/]");
        AnsiConsole.WriteLine();
    }

    private void ShowSystemInfo()
    {
        var panel = new Panel(
            new Markup(string.Join("\n",
                $"[bold]ОС:[/]           {Markup.Escape(_sysInfo.OsEdition)}",
                $"[bold]Версия:[/]       {Markup.Escape(_sysInfo.OsVersion)}",
                $"[bold]Сборка:[/]       {Markup.Escape(_sysInfo.OsBuild)}",
                $"[bold]Windows 11:[/]   {(_sysInfo.IsWindows11 ? "[green]Да[/]" : "[red]Нет[/]")}",
                $"[bold]Администратор:[/] {(_sysInfo.IsAdministrator ? "[green]Да[/]" : "[red]Нет[/]")}"
            )))
            .Header("[bold cyan]Система[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (!_sysInfo.IsWindows11)
        {
            AnsiConsole.MarkupLine(
                "[yellow bold][[!]] Обнаружена не Windows 11. Некоторые оптимизации могут быть неприменимы.[/]");
            AnsiConsole.WriteLine();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  📊 Статус оптимизаций
    // ═══════════════════════════════════════════════════════

    private async Task ShowStatusAsync()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold cyan]Статус оптимизаций[/]")
            .AddColumn(new TableColumn("[bold]Статус[/]").Centered())
            .AddColumn("[bold]Имя[/]")
            .AddColumn("[bold]Категория[/]")
            .AddColumn("[bold]Риск[/]")
            .AddColumn("[bold]Описание[/]");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Проверка состояния оптимизаций...", async _ =>
            {
                foreach (var opt in _optimizations.OrderBy(o => o.Info.Category))
                {
                    bool isApplied;
                    try
                    {
                        isApplied = await opt.IsAppliedAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ошибка проверки {Name}", opt.Info.Name);
                        isApplied = false;
                    }

                    var statusIcon = isApplied ? "[green]✓[/]" : "[grey]✗[/]";
                    var riskColor = GetRiskColor(opt.Info.RiskLevel);

                    table.AddRow(
                        statusIcon,
                        $"[white]{Markup.Escape(opt.Info.Name)}[/]",
                        $"[cyan]{opt.Info.Category}[/]",
                        $"[{riskColor}]{opt.Info.RiskLevel}[/]",
                        $"[grey]{Markup.Escape(opt.Info.Description)}[/]");
                }
            });

        AnsiConsole.Write(table);
    }

    // ═══════════════════════════════════════════════════════
    //  🚀 Применение оптимизаций
    // ═══════════════════════════════════════════════════════

    private async Task ApplyOptimizationsAsync()
    {
        // 1. Определяем, какие ещё не применены
        var notApplied = new List<IOptimization>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Проверка состояния...", async _ =>
            {
                foreach (var opt in _optimizations)
                {
                    try
                    {
                        if (!await opt.IsAppliedAsync())
                            notApplied.Add(opt);
                    }
                    catch
                    {
                        notApplied.Add(opt); // При ошибке считаем «не применена»
                    }
                }
            });

        if (notApplied.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]Все оптимизации уже применены.[/]");
            return;
        }

        // 2. Мультивыбор
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<IOptimization>()
                .Title("[bold]Выберите оптимизации для применения:[/]")
                .PageSize(15)
                .InstructionsText("[grey](Пробел — выбрать/снять, Enter — подтвердить)[/]")
                .UseConverter(FormatChoice)
                .AddChoices(notApplied.OrderBy(o => o.Info.Category)));

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Ничего не выбрано.[/]");
            return;
        }

        // 3. Показываем что будет применено
        ShowSelectionSummary("Будут применены", selected);

        // 4. Подтверждение
        if (!AnsiConsole.Confirm("[bold]Применить выбранные оптимизации?[/]"))
        {
            AnsiConsole.MarkupLine("[grey]Отменено.[/]");
            return;
        }

        // 5. Точка восстановления (опционально)
        if (AnsiConsole.Confirm("[grey]Создать точку восстановления перед применением?[/]", defaultValue: true))
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Создание точки восстановления...", async _ =>
                {
                    var rpResult = await _backup.CreateRestorePointAsync("Перед оптимизацией");
                    if (!rpResult.IsSuccess)
                        AnsiConsole.MarkupLine($"[yellow][[!]] Точка восстановления: {Markup.Escape(rpResult.ErrorMessage ?? "ошибка")}[/]");
                    else
                        AnsiConsole.MarkupLine("[green][[✓]] Точка восстановления создана[/]");
                });
        }

        // 6. Применение с прогрессом
        AnsiConsole.WriteLine();
        var results = new List<(IOptimization Opt, OptimizationResult Result)>();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                foreach (var opt in selected)
                {
                    var task = ctx.AddTask($"[white]{Markup.Escape(opt.Info.Name)}[/]");

                    try
                    {
                        var result = await opt.ApplyAsync();
                        results.Add((opt, result));
                        task.Value = 100;
                    }
                    catch (Exception ex)
                    {
                        results.Add((opt, OptimizationResult.Failure(ex.Message)));
                        task.Value = 100;
                    }
                }
            });

        // 7. Результаты
        AnsiConsole.WriteLine();
        ShowResults("Результаты применения", results);
    }

    // ═══════════════════════════════════════════════════════
    //  ↩️ Откат оптимизаций
    // ═══════════════════════════════════════════════════════

    private async Task RollbackOptimizationsAsync()
    {
        // 1. Определяем, какие применены
        var applied = new List<IOptimization>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Проверка состояния...", async _ =>
            {
                foreach (var opt in _optimizations)
                {
                    try
                    {
                        if (await opt.IsAppliedAsync())
                            applied.Add(opt);
                    }
                    catch { /* Игнорируем ошибки проверки */ }
                }
            });

        if (applied.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Нет применённых оптимизаций для отката.[/]");
            return;
        }

        // 2. Мультивыбор
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<IOptimization>()
                .Title("[bold]Выберите оптимизации для отката:[/]")
                .PageSize(15)
                .InstructionsText("[grey](Пробел — выбрать/снять, Enter — подтвердить)[/]")
                .UseConverter(FormatChoice)
                .AddChoices(applied.OrderBy(o => o.Info.Category)));

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Ничего не выбрано.[/]");
            return;
        }

        // 3. Подтверждение
        ShowSelectionSummary("Будут откачены", selected);

        if (!AnsiConsole.Confirm("[bold]Откатить выбранные оптимизации?[/]"))
        {
            AnsiConsole.MarkupLine("[grey]Отменено.[/]");
            return;
        }

        // 4. Откат с прогрессом
        var results = new List<(IOptimization Opt, OptimizationResult Result)>();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                foreach (var opt in selected)
                {
                    var task = ctx.AddTask($"[white]↩ {Markup.Escape(opt.Info.Name)}[/]");

                    try
                    {
                        var result = await opt.RollbackAsync();
                        results.Add((opt, result));
                        task.Value = 100;
                    }
                    catch (Exception ex)
                    {
                        results.Add((opt, OptimizationResult.Failure(ex.Message)));
                        task.Value = 100;
                    }
                }
            });

        // 5. Результаты
        AnsiConsole.WriteLine();
        ShowResults("Результаты отката", results);
    }

    // ═══════════════════════════════════════════════════════
    //  💾 Точка восстановления
    // ═══════════════════════════════════════════════════════

    private async Task CreateRestorePointAsync()
    {
        var description = AnsiConsole.Ask<string>("[bold]Описание точки восстановления:[/]");

        OptimizationResult? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Создание точки восстановления...", async _ =>
            {
                result = await _backup.CreateRestorePointAsync(description);
            });

        if (result!.IsSuccess)
            AnsiConsole.MarkupLine("[green][[✓]] Точка восстановления создана успешно.[/]");
        else
            AnsiConsole.MarkupLine($"[red][[✗]] {Markup.Escape(result.ErrorMessage ?? "Неизвестная ошибка")}[/]");
    }

    // ═══════════════════════════════════════════════════════
    //  📋 Управление бэкапами
    // ═══════════════════════════════════════════════════════

    private async Task ShowBackupsAsync()
    {
        var backups = await _backup.ListBackupsAsync();

        if (backups.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Нет сохранённых бэкапов.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]Бэкапы ({backups.Count})[/]")
            .AddColumn("[bold]ID[/]")
            .AddColumn("[bold]Модуль[/]")
            .AddColumn("[bold]Тип[/]")
            .AddColumn("[bold]Описание[/]")
            .AddColumn("[bold]Дата[/]");

        foreach (var entry in backups.OrderByDescending(b => b.CreatedAt))
        {
            var typeColor = entry.Type switch
            {
                BackupType.RegistryKey => "cyan",
                BackupType.SystemRestorePoint => "green",
                BackupType.File => "yellow",
                _ => "white"
            };

            table.AddRow(
                $"[grey]{Markup.Escape(entry.Id[..8])}…[/]",
                $"[white]{Markup.Escape(entry.OptimizationId)}[/]",
                $"[{typeColor}]{entry.Type}[/]",
                $"[grey]{Markup.Escape(entry.Description)}[/]",
                $"[grey]{entry.CreatedAt:yyyy-MM-dd HH:mm:ss}[/]");
        }

        AnsiConsole.Write(table);
    }

    // ═══════════════════════════════════════════════════════
    //  Вспомогательные методы
    // ═══════════════════════════════════════════════════════

    private static string FormatChoice(IOptimization opt)
    {
        var riskIcon = opt.Info.RiskLevel switch
        {
            RiskLevel.None => "⚪",
            RiskLevel.Low => "🟢",
            RiskLevel.Medium => "🟡",
            RiskLevel.High => "🔴",
            RiskLevel.Critical => "⛔",
            _ => "⚪"
        };

        return $"{riskIcon} [{opt.Info.Category}] {opt.Info.Name}";
    }

    private static string GetRiskColor(RiskLevel risk) => risk switch
    {
        RiskLevel.None => "green",
        RiskLevel.Low => "green",
        RiskLevel.Medium => "yellow",
        RiskLevel.High => "red",
        RiskLevel.Critical => "red bold",
        _ => "white"
    };

    private static void ShowSelectionSummary(string title, List<IOptimization> items)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .Title($"[bold]{title}:[/]")
            .AddColumn("[bold]Имя[/]")
            .AddColumn("[bold]Риск[/]");

        foreach (var opt in items)
        {
            var riskColor = GetRiskColor(opt.Info.RiskLevel);
            table.AddRow(
                Markup.Escape(opt.Info.Name),
                $"[{riskColor}]{opt.Info.RiskLevel}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void ShowResults(
        string title,
        List<(IOptimization Opt, OptimizationResult Result)> results)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]{title}[/]")
            .AddColumn(new TableColumn("[bold]Статус[/]").Centered())
            .AddColumn("[bold]Имя[/]")
            .AddColumn("[bold]Детали[/]");

        foreach (var (opt, result) in results)
        {
            string status;
            string details;

            if (result.IsSuccess)
            {
                status = "[green]✓[/]";
                details = result.Warnings.Count > 0
                    ? $"[yellow]{Markup.Escape(string.Join("; ", result.Warnings))}[/]"
                    : "[green]Успешно[/]";
            }
            else
            {
                status = "[red]✗[/]";
                details = $"[red]{Markup.Escape(result.ErrorMessage ?? "Ошибка")}[/]";
            }

            table.AddRow(status, Markup.Escape(opt.Info.Name), details);
        }

        AnsiConsole.Write(table);
    }
}
