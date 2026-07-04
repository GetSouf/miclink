#pragma once

#include <wdm.h>

/* Lock-free-ish ring buffer for 48 kHz mono PCM injected via IOCTL
 * and consumed by the virtual microphone capture pin. */

class MicLinkRingBuffer
{
public:
    NTSTATUS Initialize(_In_ ULONG capacityBytes);
    void     Uninitialize();

    /* Called from IOCTL dispatch (DISPATCH_LEVEL <= DISPATCH_LEVEL). */
    ULONG Write(_In_reads_bytes_(length) const UCHAR* data, _In_ ULONG length);

    /* Called from capture ReadBytes (same IRQL rules as SysVAD). */
    ULONG Read(_Out_writes_bytes_(length) UCHAR* data, _In_ ULONG length);

    void Clear();

private:
    UCHAR*  m_buffer;
    ULONG   m_capacity;
    ULONG   m_writePos;
    ULONG   m_readPos;
    ULONG   m_available;
    SHORT   m_lastSample;
    KSPIN_LOCK m_lock;
};

extern MicLinkRingBuffer g_MicLinkRingBuffer;
