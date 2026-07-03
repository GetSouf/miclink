namespace MicLinkWinUI.Infrastructure.Audio;

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class MicLinkNative
{
    public const uint MicLinkDeviceType = 0x8000;
    public const uint MicLinkIoctlIndex = 0x801;

    public static uint CTL_CODE(uint deviceType, uint function, uint method, uint access) =>
        (deviceType << 16) | (access << 14) | (function << 2) | method;

    public static readonly uint IoctlWritePcm =
        CTL_CODE(MicLinkDeviceType, MicLinkIoctlIndex, MethodBuffered, FileWriteData);

    private const uint MethodBuffered = 0;
    private const uint FileWriteData = 2;

    public const string UserDeviceName = @"\\.\MicLinkMic";
    public const string CaptureDeviceFriendlyName = "MicLink Microphone";

    public const uint GenericRead = 0x80000000;
    public const uint GenericWrite = 0x40000000;
    public const uint FileShareRead = 0x00000001;
    public const uint FileShareWrite = 0x00000002;
    public const uint OpenExisting = 3;
    public const uint FileAttributeNormal = 0x00000080;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[]? lpInBuffer,
        int nInBufferSize,
        nint lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        nint lpOverlapped);
}
