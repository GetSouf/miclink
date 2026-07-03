/* Shared IOCTL contract — kernel (WDK) and user-mode (MicLinkWinUI). */

#pragma once

#ifdef _KERNEL_MODE
#include <wdm.h>
#else
#include <winioctl.h>
#endif

#define MICLINK_DEVICE_TYPE          0x8000
#define MICLINK_IOCTL_INDEX          0x801

#define IOCTL_MICLINK_WRITE_PCM \
    CTL_CODE(MICLINK_DEVICE_TYPE, MICLINK_IOCTL_INDEX, METHOD_BUFFERED, FILE_WRITE_DATA)

#define MICLINK_KERNEL_DEVICE_NAME   L"\\Device\\MicLinkMic"
#define MICLINK_DOS_DEVICE_NAME        L"\\DosDevices\\MicLinkMic"

#define MICLINK_USER_DEVICE_NAME     L"\\\\.\\MicLinkMic"

#define MICLINK_SAMPLE_RATE          48000
#define MICLINK_CHANNELS             1
#define MICLINK_BITS_PER_SAMPLE      16
#define MICLINK_BYTES_PER_FRAME      (MICLINK_CHANNELS * MICLINK_BITS_PER_SAMPLE / 8)

/* Max PCM queued in the driver ring before oldest samples are dropped (low latency). */
#define MICLINK_MAX_LATENCY_MS       40
