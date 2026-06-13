namespace Win11Optimization.Core.Interfaces;

/// <summary>
/// Информация о текущей системе — версия ОС, права, совместимость.
/// Используется оптимизациями для проверки применимости.
/// </summary>
public interface ISystemInfo
{
    /// <summary>Полная версия ОС (например, "10.0.22631").</summary>
    string OsVersion { get; }

    /// <summary>Номер сборки (например, "22631").</summary>
    string OsBuild { get; }

    /// <summary>Название редакции (например, "Windows 11 Pro").</summary>
    string OsEdition { get; }

    /// <summary>Работает ли приложение на Windows 11 (сборка >= 22000).</summary>
    bool IsWindows11 { get; }

    /// <summary>Запущено ли приложение с правами администратора.</summary>
    bool IsAdministrator { get; }

    /// <summary>
    /// Выводит сводку о системе в форматированном виде.
    /// </summary>
    string GetSummary();
}
