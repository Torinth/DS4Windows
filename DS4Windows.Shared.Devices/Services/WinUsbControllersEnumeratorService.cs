using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using DS4Windows.Shared.Devices.HID;
using MadWizard.WinUSBNet;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace DS4Windows.Shared.Devices.Services
{
    public class WinUsbControllersEnumeratorService : IWinUsbControllersEnumeratorService
    {
        private readonly IDeviceNotificationListenerSubscriber deviceNotificationListener;

        public WinUsbControllersEnumeratorService()
        {

        }

        public void Start()
        {
            var handle = PresentationSource.FromVisual(Application.Current.MainWindow) as HwndSource;
            var notifier = new USBNotifier(handle.Handle, new Guid("{88bae032-5a81-49f0-bc3d-a4ff138216d6}"));
            
            notifier.Arrival += Notifier_Arrival;

            Devcon.FindByInterfaceGuid(new Guid("{88bae032-5a81-49f0-bc3d-a4ff138216d6}"), out var path,
                out var instanceid, 0);

        }

        private void Notifier_Arrival(object sender, USBEvent e)
        {
        }

        private void StartListening()
        {
            
        }
    }
}
