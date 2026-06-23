using System.Globalization;

namespace Win11Optimization.Core.Localization;

public static class Strings
{
    public static bool ForceEnglish { get; set; } = false;
    public static bool IsRussian => !ForceEnglish && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
    public static string Title => IsRussian ? "Главное меню" : "Main Menu";
    public static string StatusOption => IsRussian ? "📊  Статус оптимизаций" : "📊  Optimizations Status";
    public static string ApplyOption => IsRussian ? "🚀  Применить оптимизации" : "🚀  Apply Optimizations";
    public static string RollbackOption => IsRussian ? "↩️   Откатить оптимизации" : "↩️   Rollback Optimizations";
    public static string BackupOption => IsRussian ? "💾  Создать точку восстановления" : "💾  Create Restore Point";
    public static string ManageBackupsOption => IsRussian ? "📋  Управление бэкапами" : "📋  Manage Backups";
    public static string ChangeLanguageOption => IsRussian ? "🌐  Сменить язык (EN)" : "🌐  Change Language (RU)";
    public static string ExitOption => IsRussian ? "❌  Выход" : "❌  Exit";

    public static string AdminRequired => IsRussian ? "[red bold][[✗]] Запустите приложение от имени администратора.[/]" : "[red bold][[✗]] Run application as Administrator.[/]";
    public static string CheckingStatus => IsRussian ? "Проверка состояния..." : "Checking status...";
    public static string NoOptimizationsApplied => IsRussian ? "[yellow]Нет применённых оптимизаций для отката.[/]" : "[yellow]No applied optimizations to rollback.[/]";
    public static string SelectToApply => IsRussian ? "[bold]Выберите оптимизации для применения:[/]" : "[bold]Select optimizations to apply:[/]";
    public static string SelectToRollback => IsRussian ? "[bold]Выберите оптимизации для отката:[/]" : "[bold]Select optimizations to rollback:[/]";
    public static string ConfirmApply => IsRussian ? "[bold]Применить выбранные оптимизации?[/]" : "[bold]Apply selected optimizations?[/]";
    public static string ConfirmRollback => IsRussian ? "[bold]Откатить выбранные оптимизации?[/]" : "[bold]Rollback selected optimizations?[/]";
    public static string Canceled => IsRussian ? "[grey]Отменено.[/]" : "[grey]Canceled.[/]";
    public static string CategorySystem => IsRussian ? "Система" : "System";
    public static string CategoryNetwork => IsRussian ? "Сеть" : "Network";
    public static string CategoryGaming => IsRussian ? "Игры" : "Gaming";
    public static string CategoryPrivacy => IsRussian ? "Приватность" : "Privacy";
}
