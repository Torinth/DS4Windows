using DS4Windows.Shared.Devices.Util;
using MaterialDesignExtensions.Controls;
using System;
using System.ComponentModel;
using System.Windows;
using DS4Windows.Shared.Devices.Services;

namespace DS4Windows.Client.Modules.Main
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MaterialWindow, IMainView
    {
        private readonly IDeviceNotificationListener deviceNotificationListener;
        private readonly IWinUsbControllersEnumeratorService winUsbControllersEnumeratorService;

        public MainWindow(IDeviceNotificationListener deviceNotificationListener, IWinUsbControllersEnumeratorService winUsbControllersEnumeratorService)
        {
            InitializeComponent();
            this.deviceNotificationListener = deviceNotificationListener;
            this.winUsbControllersEnumeratorService = winUsbControllersEnumeratorService;
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hidGuid = new Guid();

            NativeMethods.HidD_GetHidGuid(ref hidGuid);

            deviceNotificationListener.StartListen(this, hidGuid);
            winUsbControllersEnumeratorService.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            deviceNotificationListener.EndListen();
            base.OnClosing(e);
        }
    }
}
