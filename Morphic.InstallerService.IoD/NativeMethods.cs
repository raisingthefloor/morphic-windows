using System.Runtime.InteropServices;

namespace Morphic.InstallerService.IoD
{
    public enum RestorePointEventType
    {
        BeginSystemChange = 100,
        EndSystemChange,
        BeginNestedSystemChange,
        EndNestedSystemChange,
    }
    public enum RestoreType
    {
        ApplicationInstall = 0,
        ApplicationUninstall = 1,
        ModifySettings = 12,
        CancelledOperation = 13,
        Restore = 6,
        Checkpoint = 7,
        DeviceDriverInstall = 10,
        FirstRun = 11,
        BackupRecovery = 14
    }

    [StructLayout(LayoutKind.Explicit, Size = 12)]
    public struct StateManagerStatus
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(4)] public long SequenceNumber;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RestorePointInfo
    {
        public RestorePointEventType EventType;
        public RestoreType RestorePointType;
        public long SequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;
    }

    public class NativeMethods
    {
        [DllImport("srclient.dll", CharSet = CharSet.Unicode, EntryPoint = "SRSetRestorePointW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SRSetRestorePointW([In] ref RestorePointInfo pRestorePtSpec, out StateManagerStatus pSMgrStatus);
    }
}
