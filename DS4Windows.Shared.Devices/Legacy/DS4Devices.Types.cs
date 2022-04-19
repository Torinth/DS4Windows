using System;
using DS4Windows.Shared.Devices.HID;
using DS4Windows.Shared.Devices.HID.Devices;
using PropertyChanged;

namespace DS4Windows.Shared.Devices.Legacy
{
    [AddINotifyPropertyChangedInterface]
    public class RequestElevationArgs : EventArgs
    {
        public const int STATUS_SUCCESS = 0;
        public const int STATUS_INIT_FAILURE = -1;

        public RequestElevationArgs(string instanceId)
        {
            InstanceId = instanceId;
        }

        public int StatusCode { get; set; } = STATUS_INIT_FAILURE;

        public string InstanceId { get; }
    }

    public delegate void RequestElevationDelegate(RequestElevationArgs args);

    [AddINotifyPropertyChangedInterface]
    public class CheckVirtualInfo : EventArgs
    {
        public string DeviceInstanceId { get; set; }

        public string PropertyValue { get; set; }
    }

    public delegate CheckVirtualInfo CheckVirtualDelegate(string deviceInstanceId);

    public delegate ConnectionType CheckConnectionDelegate(ICompatibleHidDevice hidDevice);

    public delegate void PrepareInitDelegate(DualShock4CompatibleHidDevice device);

    public delegate bool CheckPendingDevice(ICompatibleHidDevice device, VidPidInfo vidPidInfo);
}