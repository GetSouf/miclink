/* Control device for IOCTL_MICLINK_WRITE_PCM (MicLink Microphone feeder). */

#include <wdm.h>
#include <wdmsec.h>
#include <portcls.h>
#include "../include/miclink_ioctl.h"
#include "MicLinkRingBuffer.h"

static PDEVICE_OBJECT g_MicLinkControlDevice = nullptr;
static PDRIVER_DISPATCH g_OriginalDeviceControl = nullptr;
static PDRIVER_DISPATCH g_OriginalCreate = nullptr;
static PDRIVER_DISPATCH g_OriginalClose = nullptr;

static NTSTATUS MicLinkControlCreateClose(_In_ PDEVICE_OBJECT DeviceObject, _Inout_ PIRP Irp)
{
    if (DeviceObject == g_MicLinkControlDevice)
    {
        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;
    }

    if (g_OriginalCreate != nullptr)
    {
        return g_OriginalCreate(DeviceObject, Irp);
    }

    return PcDispatchIrp(DeviceObject, Irp);
}

static NTSTATUS MicLinkControlDispatch(_In_ PDEVICE_OBJECT DeviceObject, _Inout_ PIRP Irp)
{
    if (DeviceObject != g_MicLinkControlDevice)
    {
        if (g_OriginalDeviceControl != nullptr)
        {
            return g_OriginalDeviceControl(DeviceObject, Irp);
        }

        return PcDispatchIrp(DeviceObject, Irp);
    }

    PIO_STACK_LOCATION stack = IoGetCurrentIrpStackLocation(Irp);
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    ULONG_PTR information = 0;

    if (stack->Parameters.DeviceIoControl.IoControlCode == IOCTL_MICLINK_WRITE_PCM)
    {
        ULONG length = stack->Parameters.DeviceIoControl.InputBufferLength;
        PVOID buffer = Irp->AssociatedIrp.SystemBuffer;

        if (buffer != nullptr && length > 0)
        {
            information = g_MicLinkRingBuffer.Write(static_cast<const UCHAR*>(buffer), length);
            status = STATUS_SUCCESS;
        }
        else
        {
            status = STATUS_INVALID_PARAMETER;
        }
    }

    Irp->IoStatus.Status = status;
    Irp->IoStatus.Information = information;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);
    return status;
}

extern "C" NTSTATUS MicLinkControlDeviceRegister(_In_ PDRIVER_OBJECT DriverObject)
{
    UNICODE_STRING deviceName;
    UNICODE_STRING symLink;
    RtlInitUnicodeString(&deviceName, MICLINK_KERNEL_DEVICE_NAME);
    RtlInitUnicodeString(&symLink, MICLINK_DOS_DEVICE_NAME);

    NTSTATUS status = g_MicLinkRingBuffer.Initialize(MICLINK_SAMPLE_RATE * MICLINK_BYTES_PER_FRAME);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    status = IoCreateDeviceSecure(
        DriverObject,
        0,
        &deviceName,
        FILE_DEVICE_UNKNOWN,
        FILE_DEVICE_SECURE_OPEN,
        FALSE,
        &SDDL_DEVOBJ_SYS_ALL_ADM_RWX_WORLD_RW_RES_R,
        nullptr,
        &g_MicLinkControlDevice);

    if (!NT_SUCCESS(status))
    {
        g_MicLinkRingBuffer.Uninitialize();
        return status;
    }

    g_MicLinkControlDevice->Flags |= DO_BUFFERED_IO;
    g_MicLinkControlDevice->Flags &= ~DO_DEVICE_INITIALIZING;

    status = IoCreateSymbolicLink(&symLink, &deviceName);
    if (!NT_SUCCESS(status))
    {
        IoDeleteDevice(g_MicLinkControlDevice);
        g_MicLinkControlDevice = nullptr;
        g_MicLinkRingBuffer.Uninitialize();
        return status;
    }

    g_OriginalCreate = DriverObject->MajorFunction[IRP_MJ_CREATE];
    g_OriginalClose = DriverObject->MajorFunction[IRP_MJ_CLOSE];
    g_OriginalDeviceControl = DriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL];

    DriverObject->MajorFunction[IRP_MJ_CREATE] = MicLinkControlCreateClose;
    DriverObject->MajorFunction[IRP_MJ_CLOSE] = MicLinkControlCreateClose;
    DriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = MicLinkControlDispatch;

    return STATUS_SUCCESS;
}

extern "C" void MicLinkControlDeviceUnregister()
{
    UNICODE_STRING symLink;
    RtlInitUnicodeString(&symLink, MICLINK_DOS_DEVICE_NAME);
    IoDeleteSymbolicLink(&symLink);

    if (g_MicLinkControlDevice != nullptr)
    {
        IoDeleteDevice(g_MicLinkControlDevice);
        g_MicLinkControlDevice = nullptr;
    }

    g_MicLinkRingBuffer.Uninitialize();
}
