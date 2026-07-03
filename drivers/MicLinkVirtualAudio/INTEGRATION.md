# Integrating MicLink IOCTL into SysVAD / Virtual-Audio-Driver

## 1. Add source files

Copy into the WDK audio driver project:

- `include/miclink_ioctl.h`
- `src/MicLinkRingBuffer.h`
- `src/MicLinkRingBuffer.cpp`
- `src/MicLinkControlDevice.cpp`

## 2. Register control device

In `adapter.cpp` after the driver object is created:

```cpp
#include "MicLinkRingBuffer.h"

// DriverEntry or AddDevice after DriverObject is available:
MicLinkControlDeviceRegister(DriverObject);

// On driver unload:
MicLinkControlDeviceUnregister();
```

## 3. Feed capture pin from ring buffer

In `minwavertstream.cpp`, in `ReadBytes` for the **microphone capture** endpoint only:

```cpp
#include "MicLinkRingBuffer.h"

// Replace tone generator / SaveData with:
g_MicLinkRingBuffer.Read(m_pDmaBuffer + bufferOffset, runWrite);
```

## 4. Friendly name

In `VirtualAudioDriver.inx` / `.rc` strings, set the capture endpoint to **MicLink Microphone**.

## 5. Build & ship

1. Build x64 Release → `MicLinkVirtualAudio.sys`
2. Copy `.sys` + `install/MicLinkVirtualAudio.inf` to `Pc/MicLinkWinUI/MicLinkWinUI/Assets/Driver/`
3. Sign driver (test signing for dev, attestation for GitHub releases)

## User-mode contract

MicLink WinUI opens `\\.\MicLinkMic` and sends 48 kHz mono 16-bit PCM via `IOCTL_MICLINK_WRITE_PCM` (see `miclink_ioctl.h` and `MicLinkNative.cs`).
