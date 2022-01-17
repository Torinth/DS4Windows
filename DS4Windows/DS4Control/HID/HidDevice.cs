﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using DS4WinWPF.DS4Control.Util;
using JetBrains.Annotations;
using PInvoke;

namespace DS4WinWPF.DS4Control.HID
{
    /// <summary>
    ///     Describes a HID device's basic properties.
    /// </summary>
    internal sealed class HidDevice : IEquatable<HidDevice>, IDisposable
    {
        private readonly IntPtr inputOverlapped;

        private readonly ManualResetEvent inputReportEvent;

        /// <summary>
        ///     True if device originates from a software device.
        /// </summary>
        public bool IsVirtual;

        public HidDevice()
        {
            inputReportEvent = new ManualResetEvent(false);
            inputOverlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
            Marshal.StructureToPtr(
                new NativeOverlapped { EventHandle = inputReportEvent.SafeWaitHandle.DangerousGetHandle() },
                inputOverlapped, false);
        }

        /// <summary>
        ///     Native handle to device.
        /// </summary>
        public Kernel32.SafeObjectHandle DeviceHandle { get; private set; }

        /// <summary>
        ///     The Instance ID of this device.
        /// </summary>
        public string InstanceId { get; init; }

        /// <summary>
        ///     The path (symbolic link) of the device instance.
        /// </summary>
        public string Path { get; init; }

        /// <summary>
        ///     Device description.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        ///     Device friendly name.
        /// </summary>
        [CanBeNull]
        public string DisplayName { get; init; }

        /// <summary>
        ///     The Instance ID of the parent device.
        /// </summary>
        public string ParentInstance { get; init; }

        /// <summary>
        ///     HID Device Attributes.
        /// </summary>
        public Hid.HiddAttributes Attributes { get; init; }

        /// <summary>
        ///     HID Device Capabilities.
        /// </summary>
        public Hid.HidpCaps Capabilities { get; init; }

        /// <summary>
        ///     The manufacturer string.
        /// </summary>
        public string ManufacturerString { get; init; }

        /// <summary>
        ///     The product name.
        /// </summary>
        public string ProductString { get; init; }

        /// <summary>
        ///     Is this device currently open (for reading, writing).
        /// </summary>
        public bool IsOpen => DeviceHandle is not null && !DeviceHandle.IsClosed;

        public void Dispose()
        {
            DeviceHandle?.Dispose();
        }

        public bool Equals(HidDevice other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(InstanceId, other.InstanceId, StringComparison.OrdinalIgnoreCase);
        }

        [DllImport("hid.dll")]
        internal static extern bool HidD_SetOutputReport(IntPtr hidDeviceObject, byte[] lpReportBuffer,
            int reportBufferLength);

        [DllImport("hid.dll")]
        internal static extern bool HidD_SetFeature(IntPtr hidDeviceObject, byte[] lpReportBuffer,
            int reportBufferLength);

        [DllImport("hid.dll")]
        internal static extern bool HidD_GetFeature(IntPtr hidDeviceObject, byte[] lpReportBuffer,
            int reportBufferLength);

        /// <summary>
        ///     Access device and keep handle open until <see cref="CloseDevice" /> is called or object gets disposed.
        /// </summary>
        public void OpenDevice()
        {
            if (IsOpen || DeviceHandle.IsInvalid)
                DeviceHandle.Close();

            DeviceHandle = OpenHandle(Path);
        }

        public void CloseDevice()
        {
            if (!IsOpen) return;

            DeviceHandle?.Dispose();
        }

        public bool WriteFeatureReport(byte[] data)
        {
            return HidD_SetFeature(DeviceHandle.DangerousGetHandle(), data, data.Length);
        }

        public bool WriteOutputReportViaControl(byte[] outputBuffer)
        {
            return HidD_SetOutputReport(DeviceHandle.DangerousGetHandle(), outputBuffer, outputBuffer.Length);
        }

        public bool ReadFeatureData(byte[] inputBuffer)
        {
            return HidD_GetFeature(DeviceHandle.DangerousGetHandle(), inputBuffer, inputBuffer.Length);
        }

        public bool WriteOutputReportViaInterrupt(byte[] outputBuffer, int timeout)
        {
            var unmanagedBuffer = Marshal.AllocHGlobal(outputBuffer.Length);

            Marshal.Copy(outputBuffer, 0, unmanagedBuffer, outputBuffer.Length);

            try
            {
                DeviceHandle.OverlappedWriteFile(unmanagedBuffer, outputBuffer.Length, out _);
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedBuffer);
            }

            return true;
        }

        public void ReadInputReport(IntPtr inputBuffer, int bufferSize, out int bytesReturned)
        {
            if (inputBuffer == IntPtr.Zero)
                throw new ArgumentNullException(nameof(inputBuffer), @"Passed uninitialized memory");

            int? bytesRead = 0;

            Kernel32.ReadFile(
                DeviceHandle,
                inputBuffer,
                bufferSize,
                ref bytesRead,
                inputOverlapped);

            if (!Kernel32.GetOverlappedResult(DeviceHandle, inputOverlapped, out bytesReturned, true))
                throw new Win32Exception(Kernel32.GetLastError(), "Reading input report failed.");
        }

        private Kernel32.SafeObjectHandle OpenHandle(string devicePathName, bool openExclusive = false,
            bool enumerateOnly = false)
        {
            return Kernel32.CreateFile(devicePathName,
                enumerateOnly
                    ? 0
                    : Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ | Kernel32.ACCESS_MASK.GenericRight.GENERIC_WRITE,
                Kernel32.FileShare.FILE_SHARE_READ | Kernel32.FileShare.FILE_SHARE_WRITE,
                IntPtr.Zero, openExclusive ? 0 : Kernel32.CreationDisposition.OPEN_EXISTING,
                Kernel32.CreateFileFlags.FILE_ATTRIBUTE_NORMAL
                | Kernel32.CreateFileFlags.FILE_FLAG_NO_BUFFERING
                | Kernel32.CreateFileFlags.FILE_FLAG_WRITE_THROUGH
                | Kernel32.CreateFileFlags.FILE_FLAG_OVERLAPPED,
                Kernel32.SafeObjectHandle.Null
            );
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is HidDevice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(InstanceId);
        }

        public override string ToString()
        {
            return $"{DisplayName ?? "<no name>"} ({InstanceId})";
        }
    }
}