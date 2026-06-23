namespace Win11Optimization.Core.Interfaces;
public interface ISystemInfo
{
    string OsVersion { get; }
    string OsBuild { get; }
    string OsEdition { get; }
    bool IsWindows11 { get; }
    bool IsAdministrator { get; }
    string GetSummary();
}
