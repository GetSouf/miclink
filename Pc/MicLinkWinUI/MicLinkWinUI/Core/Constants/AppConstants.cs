namespace MicLinkWinUI.Core.Constants;

public static class AppConstants
{
    public const string AppName = "MicLink";
    public const string VirtualMicName = "MicLink Microphone";
    public const string VirtualCameraName = "MicLink Camera";
    public const string VirtualAudioHardwareId = @"ROOT\VirtualAudioDriver";

    public const string MdnsServiceType = "_miclink._tcp";
    public const int DefaultPort = 9847;
    public const int AudioPort = 9848;

    public const string SettingsKeyTheme = "Theme";
    public const string SettingsKeyMonitorSpeakers = "MonitorOnSpeakers";
    public const string SettingsKeyEffectsChain = "EffectsChain";
}
