#include "MicLinkRingBuffer.h"

MicLinkRingBuffer g_MicLinkRingBuffer;

NTSTATUS MicLinkRingBuffer::Initialize(_In_ ULONG capacityBytes)
{
    if (capacityBytes == 0)
    {
        return STATUS_INVALID_PARAMETER;
    }

    m_buffer = static_cast<UCHAR*>(ExAllocatePool2(POOL_FLAG_NON_PAGED, capacityBytes, 'kciM'));
    if (m_buffer == nullptr)
    {
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    m_capacity = capacityBytes;
    m_writePos = 0;
    m_readPos = 0;
    m_available = 0;
    KeInitializeSpinLock(&m_lock);
    return STATUS_SUCCESS;
}

void MicLinkRingBuffer::Uninitialize()
{
    if (m_buffer != nullptr)
    {
        ExFreePoolWithTag(m_buffer, 'kciM');
        m_buffer = nullptr;
    }

    m_capacity = 0;
    m_writePos = 0;
    m_readPos = 0;
    m_available = 0;
}

void MicLinkRingBuffer::Clear()
{
    KIRQL oldIrql;
    KeAcquireSpinLock(&m_lock, &oldIrql);
    m_writePos = 0;
    m_readPos = 0;
    m_available = 0;
    KeReleaseSpinLock(&m_lock, oldIrql);
}

ULONG MicLinkRingBuffer::Write(_In_reads_bytes_(length) const UCHAR* data, _In_ ULONG length)
{
    if (m_buffer == nullptr || data == nullptr || length == 0)
    {
        return 0;
    }

    KIRQL oldIrql;
    KeAcquireSpinLock(&m_lock, &oldIrql);

    ULONG written = 0;
    while (written < length && m_available < m_capacity)
    {
        m_buffer[m_writePos] = data[written];
        m_writePos = (m_writePos + 1) % m_capacity;
        ++m_available;
        ++written;
    }

    KeReleaseSpinLock(&m_lock, oldIrql);
    return written;
}

ULONG MicLinkRingBuffer::Read(_Out_writes_bytes_(length) UCHAR* data, _In_ ULONG length)
{
    if (m_buffer == nullptr || data == nullptr || length == 0)
    {
        return 0;
    }

    KIRQL oldIrql;
    KeAcquireSpinLock(&m_lock, &oldIrql);

    ULONG read = 0;
    while (read < length)
    {
        if (m_available == 0)
        {
            break;
        }

        data[read] = m_buffer[m_readPos];
        m_readPos = (m_readPos + 1) % m_capacity;
        --m_available;
        ++read;
    }

    /* Pad remainder with silence when underrun. */
    while (read < length)
    {
        data[read++] = 0;
    }

    KeReleaseSpinLock(&m_lock, oldIrql);
    return read;
}
