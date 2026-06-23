using System.Runtime.InteropServices;

namespace Win11Optimization.Services.Backup;
internal static class NativeMethods
{
    internal const int BEGIN_SYSTEM_CHANGE = 100;
    internal const int END_SYSTEM_CHANGE = 101;
    internal const int MODIFY_SETTINGS = 12;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct RESTOREPOINTINFO
    {
        public int dwEventType;
        public int dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct STATEMGRSTATUS
    {
        public int nStatus;
        public long llSequenceNumber;
    }
    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SRSetRestorePointW(
        ref RESTOREPOINTINFO pRestorePtSpec,
        out STATEMGRSTATUS pSMgrStatus);
}
