using System.Runtime.InteropServices;

namespace Win11Optimization.Services.Backup;

/// <summary>
/// P/Invoke-обёртки для Windows System Restore API (srclient.dll).
/// 
/// Документация Win32 API:
/// https://learn.microsoft.com/en-us/windows/win32/api/srrestoreptapi/
/// 
/// Используется для создания точек восстановления системы
/// без зависимости от PowerShell или WMI.
/// </summary>
internal static class NativeMethods
{
    // ── Константы событий ──────────────────────────────────

    /// <summary>Начало системного изменения — создание точки восстановления.</summary>
    internal const int BEGIN_SYSTEM_CHANGE = 100;

    /// <summary>Конец системного изменения — финализация точки.</summary>
    internal const int END_SYSTEM_CHANGE = 101;

    // ── Типы точек восстановления ──────────────────────────

    /// <summary>Изменение системных настроек (подходит для оптимизаций).</summary>
    internal const int MODIFY_SETTINGS = 12;

    // ── Структуры ──────────────────────────────────────────

    /// <summary>
    /// Параметры точки восстановления.
    /// Передаётся в SRSetRestorePointW для создания/завершения точки.
    /// 
    /// Внимание: szDescription ограничено 256 символами (включая \0).
    /// При превышении строка будет обрезана маршалером.
    /// 
    /// Layout: Sequential обязателен для корректного маршалинга в native struct.
    /// CharSet: Unicode — API работает только с wide strings (суффикс W).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct RESTOREPOINTINFO
    {
        /// <summary>BEGIN_SYSTEM_CHANGE (100) или END_SYSTEM_CHANGE (101).</summary>
        public int dwEventType;

        /// <summary>Тип точки: MODIFY_SETTINGS (12) для наших целей.</summary>
        public int dwRestorePtType;

        /// <summary>Sequence number — заполняется системой, передаём 0.</summary>
        public long llSequenceNumber;

        /// <summary>Описание точки восстановления (до 256 символов Unicode).</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    /// <summary>
    /// Результат операции System Restore.
    /// nStatus == 0 — успех, иначе Win32 error code.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct STATEMGRSTATUS
    {
        /// <summary>0 = успех. Иначе — код ошибки Win32 (например, ERROR_SERVICE_DISABLED).</summary>
        public int nStatus;

        /// <summary>Sequence number созданной точки восстановления.</summary>
        public long llSequenceNumber;
    }

    // ── Функции ────────────────────────────────────────────

    /// <summary>
    /// Создаёт или отменяет точку восстановления системы.
    /// 
    /// Требования:
    /// - Права администратора
    /// - Включённая служба "System Restore" (srservice)
    /// - Включённая защита системы для хотя бы одного диска
    /// 
    /// При отключённой службе вернёт success=false, nStatus=ERROR_SERVICE_DISABLED (1058).
    /// </summary>
    /// <param name="pRestorePtSpec">Параметры точки восстановления (ref — API может записать sequence number).</param>
    /// <param name="pSMgrStatus">Результат операции с кодом ошибки и sequence number.</param>
    /// <returns>true — операция выполнена (проверяйте nStatus), false — критическая ошибка.</returns>
    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SRSetRestorePointW(
        ref RESTOREPOINTINFO pRestorePtSpec,
        out STATEMGRSTATUS pSMgrStatus);
}
