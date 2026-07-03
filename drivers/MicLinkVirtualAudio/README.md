# MicLink Virtual Audio Driver

Kernel-mode virtual microphone for Windows 10/11, similar to [WO Mic](https://wolicheng.com/womic/). MicLink PC app injects PCM from the phone into **MicLink Microphone** — no VB-Audio or other third-party tools.

## How it works

1. `MicLinkVirtualAudio.sys` registers a Windows audio capture endpoint named **MicLink Microphone**.
2. MicLink WinUI opens `\\.\MicLinkMic` and sends PCM frames via `IOCTL_MICLINK_WRITE_PCM`.
3. The driver stores samples in a ring buffer; the capture pin reads them for Discord, OBS, Zoom, etc.

## Build (requires Windows Driver Kit)

The driver is based on the [Microsoft SysVAD sample](https://github.com/microsoft/Windows-driver-samples/tree/main/audio/sysvad) (MS-PL).

1. Install Visual Studio 2022 + **Windows Driver Kit (WDK)**.
2. Clone Microsoft driver samples:
   ```powershell
   git clone --depth 1 https://github.com/microsoft/Windows-driver-samples.git
   ```
3. Copy `drivers/MicLinkVirtualAudio/src/*` and `include/miclink_ioctl.h` into the SysVAD TabletAudioSample project (or fork [Virtual-Audio-Driver](https://github.com/VirtualDrivers/Virtual-Audio-Driver) — MIT).
4. Integrate:
   - Call `MicLinkControlDeviceRegister(DriverObject)` from adapter PnP start.
   - In **microphone capture** `ReadBytes`, call `g_MicLinkRingBuffer.Read(...)` instead of the tone generator.
   - Set friendly names in `.inx` / `.rc` to **MicLink Microphone**.
5. Build x64 Release, sign the driver (test certificate for dev, attestation for release).
6. Copy `MicLinkVirtualAudio.sys` + `MicLinkVirtualAudio.inf` to:
   - `drivers/MicLinkVirtualAudio/install/`
   - `Pc/MicLinkWinUI/MicLinkWinUI/Assets/Driver/`

### Development signing

```powershell
bcdedit /set testsigning on
```

Reboot, then install from MicLink Settings or:

```powershell
pnputil /add-driver .\MicLinkVirtualAudio.inf /install
```

## IOCTL contract

See `include/miclink_ioctl.h` — shared with `MicLinkWinUI.Infrastructure.Audio.MicLinkNative`.

## License

- MicLink overlay files: MIT (same as MicLink app).
- SysVAD-derived code: Microsoft Public License (MS-PL) when integrated with WDK samples.
