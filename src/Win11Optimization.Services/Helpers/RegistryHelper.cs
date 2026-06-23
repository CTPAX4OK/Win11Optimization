using Microsoft.Win32;

namespace Win11Optimization.Services.Helpers;
internal static class RegistryHelper
{
    public static int? GetDword(RegistryKey baseKey, string subKeyPath, string valueName)
    {
        using var key = baseKey.OpenSubKey(subKeyPath);
        var value = key?.GetValue(valueName);
        return value is int intValue ? intValue : null;
    }
    public static void SetDword(RegistryKey baseKey, string subKeyPath, string valueName, int value)
    {
        using var key = baseKey.OpenSubKey(subKeyPath, writable: true)
            ?? baseKey.CreateSubKey(subKeyPath);
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }
    public static void DeleteValue(RegistryKey baseKey, string subKeyPath, string valueName)
    {
        using var key = baseKey.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
