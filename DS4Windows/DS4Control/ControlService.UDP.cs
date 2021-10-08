﻿using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using DS4WinWPF.DS4Control.Logging;
using static DS4Windows.Global;

namespace DS4Windows
{
    public partial class ControlService
    {
        private bool udpChangeStatus;
        public bool changingUDPPort;

        public async void UseUDPPort()
        {
            changingUDPPort = true;
            var devices = DS4Devices.GetDS4Controllers();
            foreach (var dev in devices)
                dev.QueueEvent(() =>
                {
                    if (dev.MotionEvent != null) dev.Report -= dev.MotionEvent;
                });

            await Task.Delay(100);

            var UDP_SERVER_PORT = appSettings.Settings.UDPServerPort;
            var UDP_SERVER_LISTEN_ADDRESS = appSettings.Settings.UDPServerListenAddress;

            try
            {
                _udpServer.Start(UDP_SERVER_PORT, UDP_SERVER_LISTEN_ADDRESS);
                foreach (var dev in devices)
                    dev.QueueEvent(() =>
                    {
                        if (dev.MotionEvent != null) dev.Report += dev.MotionEvent;
                    });
                LogDebug($"UDP server listening on address {UDP_SERVER_LISTEN_ADDRESS} port {UDP_SERVER_PORT}");
            }
            catch (SocketException ex)
            {
                var errMsg =
                    string.Format(
                        "Couldn't start UDP server on address {0}:{1}, outside applications won't be able to access pad data ({2})",
                        UDP_SERVER_LISTEN_ADDRESS, UDP_SERVER_PORT, ex.SocketErrorCode);

                LogDebug(errMsg, true);
                AppLogger.Instance.LogToTray(errMsg, true, true);
            }

            changingUDPPort = false;
        }

        public void ResetUdpSmoothingFilters(int idx)
        {
            if (idx >= UdpServer.NUMBER_SLOTS) return;

            var temp = udpEuroPairAccel[idx] = new OneEuroFilter3D();
            temp.SetFilterAttrs(appSettings.Settings.UDPServerSmoothingOptions.MinCutoff,
                appSettings.Settings.UDPServerSmoothingOptions.Beta);

            temp = udpEuroPairGyro[idx] = new OneEuroFilter3D();
            temp.SetFilterAttrs(appSettings.Settings.UDPServerSmoothingOptions.MinCutoff,
                appSettings.Settings.UDPServerSmoothingOptions.Beta);
        }

        private void ChangeUdpSmoothingAttrs()
        {
            foreach (var filter3D in udpEuroPairAccel)
            {
                filter3D.SetFilterAttrs(appSettings.Settings.UDPServerSmoothingOptions.MinCutoff,
                    appSettings.Settings.UDPServerSmoothingOptions.Beta);
            }

            foreach (var filter3D in udpEuroPairGyro)
            {
                filter3D.SetFilterAttrs(appSettings.Settings.UDPServerSmoothingOptions.MinCutoff,
                    appSettings.Settings.UDPServerSmoothingOptions.Beta);
            }
        }
    }
}
