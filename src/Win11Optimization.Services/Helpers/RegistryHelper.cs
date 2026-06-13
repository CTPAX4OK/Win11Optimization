using Microsoft.Win32;

namespace Win11Optimization.Services.Helpers;

/// <summary>
/// Утилиты для работы с реестром Windows.
/// Тонкая обёртка над Microsoft.Win32.Registry API для типичных операций.
/// 
/// Все методы работают синхронно (Registry API синхронный).
/// Для HKLM требуются права администратора на запись.
/// </summary>
internal static class RegistryHelper
{
    /// <summary>
    /// Читает DWORD (REG_DWORD) значение из реестра.
    /// </summary>
    /// <param name="baseKey">Базовый ключ (например, Registry.LocalMachine).</param>
    /// <param name="subKeyPath">Путь подключа (например, "SYSTEM\CurrentControlSet\...").</param>
    /// <param name="valueName">Имя значения.</param>
    /// <returns>Значение DWORD или null, если ключ/значение не найдены.</returns>
    public static int? GetDword(RegistryKey baseKey, string subKeyPath, string valueName)
    {
        using var key = baseKey.OpenSubKey(subKeyPath);
        var value = key?.GetValue(valueName);
        return value is int intValue ? intValue : null;
    }

    /// <summary>
    /// Записывает DWORD (REG_DWORD) значение в реестр.
    /// Создаёт ключ, если он не существует.
    /// 
    /// Требует прав администратора для записи в HKLM.
    /// </summary>
    /// <param name="baseKey">Базовый ключ.</param>
    /// <param name="subKeyPath">Путь подключа.</param>
    /// <param name="valueName">Имя значения.</param>
    /// <param name="value">Значение DWORD.</param>
    public static void SetDword(RegistryKey baseKey, string subKeyPath, string valueName, int value)
    {
        using var key = baseKey.OpenSubKey(subKeyPath, writable: true)
            ?? baseKey.CreateSubKey(subKeyPath);
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    /// <summary>
    /// Удаляет значение из реестра. Не бросает исключение, если значение отсутствует.
    /// </summary>
    public static void DeleteValue(RegistryKey baseKey, string subKeyPath, string valueName)
    {
        using var key = baseKey.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
