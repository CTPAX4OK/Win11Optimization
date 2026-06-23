using Microsoft.Extensions.Logging;
using Spectre.Console;
using Win11Optimization.Core.Interfaces;
using Win11Optimization.Core.Models;
using Win11Optimization.Core.Localization;

namespace Win11Optimization.CLI;
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

    public async Task<int> RunAsync()
    {
        ShowBanner();
        ShowSystemInfo();

        AnsiConsole.MarkupLine($"[green][[✓]][/] {(Strings.IsRussian ? "Модули оптимизации" : "Optimization modules")}: [cyan]{_optimizations.Count}[/]");
        AnsiConsole.WriteLine();
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold cyan]═══ {Strings.Title} ═══[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(
                        Strings.StatusOption,
                        Strings.ApplyOption,
                        Strings.RollbackOption,
                        Strings.BackupOption,
                        Strings.ManageBackupsOption,
                        Strings.ChangeLanguageOption,
                        Strings.ExitOption));

            AnsiConsole.WriteLine();

            if (choice == Strings.StatusOption)
                await ShowStatusAsync();
            else if (choice == Strings.ApplyOption)
                await ApplyOptimizationsAsync();
            else if (choice == Strings.RollbackOption)
                await RollbackOptimizationsAsync();
            else if (choice == Strings.BackupOption)
                await CreateRestorePointAsync();
            else if (choice == Strings.ManageBackupsOption)
                await ShowBackupsAsync();
            else if (choice == Strings.ChangeLanguageOption)
            {
                Strings.ForceEnglish = !Strings.ForceEnglish;
                AnsiConsole.MarkupLine(Strings.IsRussian ? "[green]Язык изменен на Русский.[/]" : "[green]Language changed to English.[/]");
            }
            else if (choice == Strings.ExitOption)
            {
                AnsiConsole.MarkupLine(Strings.IsRussian ? "[grey]До свидания![/]" : "[grey]Goodbye![/]");
                return 0;
            }

            AnsiConsole.WriteLine();
        }
    }

    private void ShowBanner()
    {
        AnsiConsole.Write(
            new FigletText("Win11 Optimizer")
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[grey]v1.0.0 | Модульный оптимизатор Windows 11 | MIT License[/]");
        AnsiConsole.WriteLine();
    }

    private void ShowSystemInfo()
    {
        var osLabel = Strings.IsRussian ? "ОС" : "OS";
        var versionLabel = Strings.IsRussian ? "Версия" : "Version";
        var buildLabel = Strings.IsRussian ? "Сборка" : "Build";
        var win11Label = Strings.IsRussian ? "Windows 11" : "Windows 11";
        var adminLabel = Strings.IsRussian ? "Администратор" : "Administrator";
        var yesStr = Strings.IsRussian ? "Да" : "Yes";
        var noStr = Strings.IsRussian ? "Нет" : "No";

        var panel = new Panel(
            new Markup(string.Join("\n",
                $"[bold]{osLabel}:[/]           {Markup.Escape(_sysInfo.OsEdition)}",
                $"[bold]{versionLabel}:[/]       {Markup.Escape(_sysInfo.OsVersion)}",
                $"[bold]{buildLabel}:[/]       {Markup.Escape(_sysInfo.OsBuild)}",
                $"[bold]{win11Label}:[/]   {(_sysInfo.IsWindows11 ? $"[green]{yesStr}[/]" : $"[red]{noStr}[/]")}",
                $"[bold]{adminLabel}:[/] {(_sysInfo.IsAdministrator ? $"[green]{yesStr}[/]" : $"[red]{noStr}[/]")}"
            )))
            .Header($"[bold cyan]{(Strings.IsRussian ? "Система" : "System")}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (!_sysInfo.IsWindows11)
        {
            AnsiConsole.MarkupLine(Strings.IsRussian 
                ? "[yellow bold][[!]] Обнаружена не Windows 11. Некоторые оптимизации могут быть неприменимы.[/]"
                : "[yellow bold][[!]] Not Windows 11 detected. Some optimizations may not apply.[/]");
            AnsiConsole.WriteLine();
        }
    }

    private async Task ShowStatusAsync()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]{(Strings.IsRussian ? "Статус оптимизаций" : "Optimizations Status")}[/]")
            .AddColumn(new TableColumn($"[bold]{(Strings.IsRussian ? "Статус" : "Status")}[/]").Centered())
            .AddColumn($"[bold]{(Strings.IsRussian ? "Имя" : "Name")}[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Категория" : "Category")}[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Риск" : "Risk")}[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Описание" : "Description")}[/]");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(Strings.CheckingStatus, async _ =>
            {
                var tasks = _optimizations.OrderBy(o => o.Info.Category).Select(async opt => 
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
                    return (opt, isApplied);
                });

                var results = await Task.WhenAll(tasks);

                foreach (var (opt, isApplied) in results)
                {
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

    private async Task ApplyOptimizationsAsync()
    {
        var notApplied = new List<IOptimization>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(Strings.CheckingStatus, async _ =>
            {
                var tasks = _optimizations.Select(async opt => 
                {
                    try { return (opt, await opt.IsAppliedAsync()); }
                    catch { return (opt, false); }
                });
                var results = await Task.WhenAll(tasks);
                foreach(var r in results) if(!r.Item2) notApplied.Add(r.opt);
            });

        if (notApplied.Count == 0)
        {
            AnsiConsole.MarkupLine(Strings.IsRussian ? "[green]Все оптимизации уже применены.[/]" : "[green]All optimizations are already applied.[/]");
            return;
        }
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<IOptimization>()
                .Title(Strings.SelectToApply)
                .PageSize(15)
                .InstructionsText(Strings.IsRussian ? "[grey](Пробел — выбрать/снять, Enter — подтвердить)[/]" : "[grey](Space - toggle, Enter - confirm)[/]")
                .UseConverter(FormatChoice)
                .AddChoices(notApplied.OrderBy(o => o.Info.Category)));

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine(Strings.IsRussian ? "[yellow]Ничего не выбрано.[/]" : "[yellow]Nothing selected.[/]");
            return;
        }
        ShowSelectionSummary(Strings.IsRussian ? "Будут применены" : "Will be applied", selected);
        if (!AnsiConsole.Confirm(Strings.ConfirmApply))
        {
            AnsiConsole.MarkupLine(Strings.Canceled);
            return;
        }
        if (AnsiConsole.Confirm(Strings.IsRussian ? "[grey]Создать точку восстановления перед применением?[/]" : "[grey]Create restore point before applying?[/]", defaultValue: true))
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(Strings.IsRussian ? "Создание точки восстановления..." : "Creating restore point...", async _ =>
                {
                    var rpResult = await _backup.CreateRestorePointAsync(Strings.IsRussian ? "Перед оптимизацией" : "Before optimization");
                    if (!rpResult.IsSuccess)
                        AnsiConsole.MarkupLine($"[yellow][[!]] {(Strings.IsRussian ? "Ошибка" : "Error")}: {Markup.Escape(rpResult.ErrorMessage ?? "error")}[/]");
                    else
                        AnsiConsole.MarkupLine($"[green][[✓]] {(Strings.IsRussian ? "Точка восстановления создана" : "Restore point created")}[/]");
                });
        }
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
        AnsiConsole.WriteLine();
        ShowResults(Strings.IsRussian ? "Результаты применения" : "Apply Results", results);
    }

    private async Task RollbackOptimizationsAsync()
    {
        var applied = new List<IOptimization>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(Strings.CheckingStatus, async _ =>
            {
                var tasks = _optimizations.Select(async opt => 
                {
                    try { return (opt, await opt.IsAppliedAsync()); }
                    catch { return (opt, false); }
                });
                var results = await Task.WhenAll(tasks);
                foreach(var r in results) if(r.Item2) applied.Add(r.opt);
            });

        if (applied.Count == 0)
        {
            AnsiConsole.MarkupLine(Strings.NoOptimizationsApplied);
            return;
        }
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<IOptimization>()
                .Title(Strings.SelectToRollback)
                .PageSize(15)
                .InstructionsText(Strings.IsRussian ? "[grey](Пробел — выбрать/снять, Enter — подтвердить)[/]" : "[grey](Space - toggle, Enter - confirm)[/]")
                .UseConverter(FormatChoice)
                .AddChoices(applied.OrderBy(o => o.Info.Category)));

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine(Strings.IsRussian ? "[yellow]Ничего не выбрано.[/]" : "[yellow]Nothing selected.[/]");
            return;
        }
        ShowSelectionSummary(Strings.IsRussian ? "Будут откачены" : "Will be rolled back", selected);

        if (!AnsiConsole.Confirm(Strings.ConfirmRollback))
        {
            AnsiConsole.MarkupLine(Strings.Canceled);
            return;
        }
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
        AnsiConsole.WriteLine();
        ShowResults(Strings.IsRussian ? "Результаты отката" : "Rollback Results", results);
    }

    private async Task CreateRestorePointAsync()
    {
        var description = AnsiConsole.Ask<string>(Strings.IsRussian ? "[bold]Описание точки восстановления:[/]" : "[bold]Restore point description:[/]");

        OptimizationResult? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Создание точки восстановления...", async _ =>
            {
                result = await _backup.CreateRestorePointAsync(description);
            });

        if (result!.IsSuccess)
            AnsiConsole.MarkupLine(Strings.IsRussian ? "[green][[✓]] Точка восстановления создана успешно.[/]" : "[green][[✓]] Restore point created successfully.[/]");
        else
            AnsiConsole.MarkupLine($"[red][[✗]] {Markup.Escape(result.ErrorMessage ?? "Error")}[/]");
    }

    private async Task ShowBackupsAsync()
    {
        var backups = await _backup.ListBackupsAsync();

        if (backups.Count == 0)
        {
            AnsiConsole.MarkupLine(Strings.IsRussian ? "[yellow]Нет сохранённых бэкапов.[/]" : "[yellow]No saved backups.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]{(Strings.IsRussian ? "Бэкапы" : "Backups")} ({backups.Count})[/]")
            .AddColumn("[bold]ID[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Модуль" : "Module")}[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Тип" : "Type")}[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Описание" : "Description")}[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Дата" : "Date")}[/]");

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
            .AddColumn(new TableColumn($"[bold]{(Strings.IsRussian ? "Статус" : "Status")}[/]").Centered())
            .AddColumn($"[bold]{(Strings.IsRussian ? "Имя" : "Name")}[/]")
            .AddColumn($"[bold]{(Strings.IsRussian ? "Детали" : "Details")}[/]");

        foreach (var (opt, result) in results)
        {
            string status;
            string details;

            if (result.IsSuccess)
            {
                status = "[green]✓[/]";
                details = result.Warnings.Count > 0
                    ? $"[yellow]{Markup.Escape(string.Join("; ", result.Warnings))}[/]"
                    : (Strings.IsRussian ? "[green]Успешно[/]" : "[green]Success[/]");
            }
            else
            {
                status = "[red]✗[/]";
                details = $"[red]{Markup.Escape(result.ErrorMessage ?? "Error")}[/]";
            }

            table.AddRow(status, Markup.Escape(opt.Info.Name), details);
        }

        AnsiConsole.Write(table);
    }
}
