﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DS4Windows;
using DS4WinWPF.DS4Control.Attributes;
using DS4WinWPF.DS4Control.IoC.Services;
using DS4WinWPF.DS4Control.Logging;
using DS4WinWPF.DS4Control.Util;
using DS4WinWPF.Properties;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public class ControllerListViewModel
    {
        //private object _colLockobj = new object();
        private ReaderWriterLockSlim _colListLocker = new ReaderWriterLockSlim();
        private ObservableCollection<CompositeDeviceModel> controllerCol =
            new ObservableCollection<CompositeDeviceModel>();
        private Dictionary<int, CompositeDeviceModel> controllerDict =
            new Dictionary<int, CompositeDeviceModel>();

        public ObservableCollection<CompositeDeviceModel> ControllerCol
        { get => controllerCol; set => controllerCol = value; }

        private ProfileList profileListHolder;
        private ControlService controlService;
        private int currentIndex;
        public int CurrentIndex { get => currentIndex; set => currentIndex = value; }
        public CompositeDeviceModel CurrentItem {
            get
            {
                if (currentIndex == -1) return null;
                controllerDict.TryGetValue(currentIndex, out CompositeDeviceModel item);
                return item;
            }
        }

        public Dictionary<int, CompositeDeviceModel> ControllerDict { get => controllerDict; set => controllerDict = value; }

        private readonly IAppSettingsService appSettings;

        //public ControllerListViewModel(Tester tester, ProfileList profileListHolder)
        public ControllerListViewModel(ControlService service, ProfileList profileListHolder, IAppSettingsService appSettings)
        {
            this.appSettings = appSettings;
            this.profileListHolder = profileListHolder;
            this.controlService = service;
            service.ServiceStarted += ControllersChanged;
            service.PreServiceStop += ClearControllerList;
            service.HotplugController += Service_HotplugController;
            //tester.StartControllers += ControllersChanged;
            //tester.ControllersRemoved += ClearControllerList;

            int idx = 0;
            foreach (DS4Device currentDev in controlService.slotManager.ControllerColl)
            {
                CompositeDeviceModel temp = new CompositeDeviceModel(appSettings, service, currentDev,
                    idx, Global.Instance.Config.ProfilePath[idx], profileListHolder);
                controllerCol.Add(temp);
                controllerDict.Add(idx, temp);
                currentDev.Removal += Controller_Removal;
                idx++;
            }

            //BindingOperations.EnableCollectionSynchronization(controllerCol, _colLockobj);
            BindingOperations.EnableCollectionSynchronization(controllerCol, _colListLocker,
                ColLockCallback);
        }

        private void ColLockCallback(IEnumerable collection, object context,
            Action accessMethod, bool writeAccess)
        {
            if (writeAccess)
            {
                using (WriteLocker locker = new WriteLocker(_colListLocker))
                {
                    accessMethod?.Invoke();
                }
            }
            else
            {
                using (ReadLocker locker = new ReadLocker(_colListLocker))
                {
                    accessMethod?.Invoke();
                }
            }
        }

        private void Service_HotplugController(ControlService sender,
            DS4Device device, int index)
        {
            // Engage write lock pre-maturely
            using (WriteLocker readLock = new WriteLocker(_colListLocker))
            {
                // Look if device exists. Also, check if disconnect might be occurring
                if (!controllerDict.ContainsKey(index) && !device.IsRemoving)
                {
                    CompositeDeviceModel temp = new CompositeDeviceModel(appSettings, controlService, device,
                        index, Global.Instance.Config.ProfilePath[index], profileListHolder);
                    controllerCol.Add(temp);
                    controllerDict.Add(index, temp);

                    device.Removal += Controller_Removal;
                }
            }
        }

        private void ClearControllerList(object sender, EventArgs e)
        {
            _colListLocker.EnterReadLock();
            foreach (CompositeDeviceModel temp in controllerCol)
            {
                temp.Device.Removal -= Controller_Removal;
            }
            _colListLocker.ExitReadLock();

            _colListLocker.EnterWriteLock();
            controllerCol.Clear();
            controllerDict.Clear();
            _colListLocker.ExitWriteLock();
        }

        private void ControllersChanged(object sender, EventArgs e)
        {
            //IEnumerable<DS4Device> devices = DS4Windows.DS4Devices.getDS4Controllers();
            using (var locker = new ReadLocker(controlService.slotManager.CollectionLocker))
            {
                foreach (var currentDev in controlService.slotManager.ControllerColl)
                {
                    var found = false;
                    _colListLocker.EnterReadLock();
                    foreach (var temp in controllerCol)
                        if (temp.Device == currentDev)
                        {
                            found = true;
                            break;
                        }

                    _colListLocker.ExitReadLock();

                    // Check for new device. Also, check if disconnect might be occurring
                    if (!found && !currentDev.IsRemoving)
                    {
                        //int idx = controllerCol.Count;
                        _colListLocker.EnterWriteLock();
                        var idx = controlService.slotManager.ReverseControllerDict[currentDev];
                        var temp = new CompositeDeviceModel(appSettings, controlService, currentDev,
                            idx, Global.Instance.Config.ProfilePath[idx], profileListHolder);
                        controllerCol.Add(temp);
                        controllerDict.Add(idx, temp);
                        _colListLocker.ExitWriteLock();

                        currentDev.Removal += Controller_Removal;
                    }
                }
            }
        }

        private void Controller_Removal(object sender, EventArgs e)
        {
            var currentDev = sender as DS4Device;
            CompositeDeviceModel found = null;
            _colListLocker.EnterReadLock();
            foreach (var temp in controllerCol)
                if (temp.Device == currentDev)
                {
                    found = temp;
                    break;
                }

            _colListLocker.ExitReadLock();

            if (found != null)
            {
                _colListLocker.EnterWriteLock();
                controllerCol.Remove(found);
                controllerDict.Remove(found.DevIndex);
                Application.Current.Dispatcher.Invoke(async () => { await appSettings.SaveAsync(); });

                _colListLocker.ExitWriteLock();
            }
        }
    }

    public class CompositeDeviceModel
    {
        private DS4Device device;
        private string selectedProfile;
        private ProfileList profileListHolder;
        private ProfileEntity selectedEntity;
        private int selectedIndex = 1;
        private int devIndex;

        public DS4Device Device { get => device; set => device = value; }
        public string SelectedProfile { get => selectedProfile; set => selectedProfile = value; }
        public ProfileList ProfileEntities { get => profileListHolder; set => profileListHolder = value; }
        public ObservableCollection<ProfileEntity> ProfileListCol => profileListHolder.ProfileListCollection;

        public string LightColor
        {
            get
            {
                var color = appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.UseCustomLed
                    ? appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.CustomLed
                    : appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.Led;
                return $"#FF{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
            }
        }

        public event EventHandler LightColorChanged;

        public Color CustomLightColor => appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.CustomLed.ToColor();

        public string BatteryState => $"{device.Battery}%{(device.Charging ? "+" : "")}";

        public event Action<DS4Device> BatteryStateChanged;

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (selectedIndex == value) return;
                selectedIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedIndexChanged;

        public string StatusSource
        {
            get
            {
                var imgName =
                    (string)Application.Current.FindResource(device.ConnectionType == ConnectionType.USB
                        ? "UsbImg"
                        : "BtImg");
                var source = $"/DS4Windows;component/Resources/{imgName}";
                return source;
            }
        }

        public string ExclusiveSource
        {
            get
            {
                string imgName = (string)App.Current.FindResource("CancelImg");
                string source = $"/DS4Windows;component/Resources/{imgName}";
                switch(device.CurrentExclusiveStatus)
                {
                    case DS4Device.ExclusiveStatus.Exclusive:
                        imgName = (string)App.Current.FindResource("CheckedImg");
                        source = $"/DS4Windows;component/Resources/{imgName}";
                        break;
                    case DS4Device.ExclusiveStatus.HidHideAffected:
                    case DS4Device.ExclusiveStatus.HidGuardAffected:
                        imgName = (string)App.Current.FindResource("KeyImageImg");
                        source = $"/DS4Windows;component/Resources/{imgName}";
                        break;
                    default:
                        break;
                }

                return source;
            }
        }

        public bool LinkedProfile
        {
            get => ProfilesService.Instance.ActiveProfiles.ElementAt(devIndex).IsLinkedProfile;
            set => ProfilesService.Instance.ActiveProfiles.ElementAt(devIndex).IsLinkedProfile = value;
        }

        public int DevIndex => devIndex;

        public int DisplayDevIndex => devIndex + 1;

        public string TooltipIDText => string.Format(Resources.InputDelay, device.Latency);

        public event EventHandler TooltipIDTextChanged;

        private bool useCustomColor;
        public bool UseCustomColor { get => useCustomColor; set => useCustomColor = value; }

        private ContextMenu lightContext;
        public ContextMenu LightContext { get => lightContext; set => lightContext = value; }

        public string IdText => $"{device.DisplayName} ({device.MacAddress.AsFriendlyName()})";

        public event EventHandler IdTextChanged;

        public string IsExclusiveText
        {
            get
            {
                string temp = Translations.Strings.SharedAccess;
                switch(device.CurrentExclusiveStatus)
                {
                    case DS4Device.ExclusiveStatus.Exclusive:
                        temp = Translations.Strings.ExclusiveAccess;
                        break;
                    case DS4Device.ExclusiveStatus.HidHideAffected:
                        temp = Translations.Strings.HidHideAccess;
                        break;
                    case DS4Device.ExclusiveStatus.HidGuardAffected:
                        temp = Translations.Strings.HidGuardianAccess;
                        break;
                    default:
                        break;
                }

                return temp;
            }
        }

        public bool PrimaryDevice => device.PrimaryDevice;

        public delegate void CustomColorHandler(CompositeDeviceModel sender);
        public event CustomColorHandler RequestColorPicker;

        private readonly ControlService rootHub;

        private readonly IAppSettingsService appSettings;

        public CompositeDeviceModel(IAppSettingsService appSettings, ControlService service, DS4Device device, int devIndex, string profile,
            ProfileList collection)
        {
            this.appSettings = appSettings;
            this.device = device;
            rootHub = service;
            
            device.BatteryChanged += (sender) => BatteryStateChanged?.Invoke(sender);
            device.ChargingChanged += (sender) => BatteryStateChanged?.Invoke(sender);
            device.MacAddressChanged += (sender, e) => IdTextChanged?.Invoke(this, e);

            this.devIndex = devIndex;
            this.selectedProfile = profile;
            profileListHolder = collection;
            if (!string.IsNullOrEmpty(selectedProfile))
            {
                this.selectedEntity = profileListHolder.ProfileListCollection.SingleOrDefault(x => x.Name == selectedProfile);
            }

            if (this.selectedEntity != null)
            {
                selectedIndex = profileListHolder.ProfileListCollection.IndexOf(this.selectedEntity);
                HookEvents(true);
            }

            useCustomColor = appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.UseCustomLed;
        }

        public async Task ChangeSelectedProfile()
        {
            if (this.selectedEntity != null)
            {
                HookEvents(false);
            }

            string prof = Global.Instance.Config.ProfilePath[devIndex] = ProfileListCol[selectedIndex].Name;
            if (LinkedProfile)
            {
                Global.Instance.Config.ChangeLinkedProfile(device.MacAddress, Global.Instance.Config.ProfilePath[devIndex]);
                Global.Instance.Config.SaveLinkedProfiles();
            }
            else
            {
                Global.Instance.Config.OlderProfilePath[devIndex] = Global.Instance.Config.ProfilePath[devIndex];
            }

            //Global.Save();
            await Global.Instance.LoadProfile(devIndex, true, rootHub);
            string prolog = string.Format(Properties.Resources.UsingProfile, (devIndex + 1).ToString(), prof, $"{device.Battery}");
            AppLogger.Instance.LogToGui(prolog, false);

            selectedProfile = prof;
            this.selectedEntity = profileListHolder.ProfileListCollection.SingleOrDefault(x => x.Name == prof);
            if (this.selectedEntity != null)
            {
                selectedIndex = profileListHolder.ProfileListCollection.IndexOf(this.selectedEntity);
                HookEvents(true);
            }

            LightColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void HookEvents(bool state)
        {
            if (state)
            {
                selectedEntity.ProfileSaved += SelectedEntity_ProfileSaved;
                selectedEntity.ProfileDeleted += SelectedEntity_ProfileDeleted;
            }
            else
            {
                selectedEntity.ProfileSaved -= SelectedEntity_ProfileSaved;
                selectedEntity.ProfileDeleted -= SelectedEntity_ProfileDeleted;
            }
        }

        private void SelectedEntity_ProfileDeleted(object sender, EventArgs e)
        {
            HookEvents(false);
            ProfileEntity entity = profileListHolder.ProfileListCollection.FirstOrDefault();
            if (entity != null)
            {
                SelectedIndex = profileListHolder.ProfileListCollection.IndexOf(entity);
            }
        }

        private async void SelectedEntity_ProfileSaved(object sender, EventArgs e)
        {
            await Global.Instance.LoadProfile(devIndex, false, rootHub);
            LightColorChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RequestUpdatedTooltipID()
        {
            TooltipIDTextChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SaveLinked(bool status)
        {
            if (device != null && device.IsSynced())
            {
                if (status)
                {
                    if (device.IsValidSerial())
                    {
                        Global.Instance.Config.ChangeLinkedProfile(device.MacAddress, Global.Instance.Config.ProfilePath[devIndex]);
                    }
                }
                else
                {
                    Global.Instance.Config.RemoveLinkedProfile(device.MacAddress);
                    Global.Instance.Config.ProfilePath[devIndex] = Global.Instance.Config.OlderProfilePath[devIndex];
                }

                Global.Instance.Config.SaveLinkedProfiles();
            }
        }

        [MissingLocalization]
        public void AddLightContextItems()
        {
            var thing = new MenuItem { Header = "Use Profile Color", IsChecked = !useCustomColor };
            thing.Click += ProfileColorMenuClick;
            lightContext.Items.Add(thing);
            thing = new MenuItem { Header = "Use Custom Color", IsChecked = useCustomColor };
            thing.Click += CustomColorItemClick;
            lightContext.Items.Add(thing);
        }

        private void ProfileColorMenuClick(object sender, RoutedEventArgs e)
        {
            useCustomColor = false;
            RefreshLightContext();
            appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.UseCustomLed = false;
            LightColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CustomColorItemClick(object sender, RoutedEventArgs e)
        {
            useCustomColor = true;
            RefreshLightContext();
            appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.UseCustomLed = true;
            LightColorChanged?.Invoke(this, EventArgs.Empty);
            RequestColorPicker?.Invoke(this);
        }

        private void RefreshLightContext()
        {
            (lightContext.Items[0] as MenuItem).IsChecked = !useCustomColor;
            (lightContext.Items[1] as MenuItem).IsChecked = useCustomColor;
        }

        public void UpdateCustomLightColor(Color color)
        {
            appSettings.Settings.LightbarSettingInfo[devIndex].Ds4WinSettings.CustomLed = new DS4Color(color);
            LightColorChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ChangeSelectedProfile(string loadprofile)
        {
            ProfileEntity temp = profileListHolder.ProfileListCollection.SingleOrDefault(x => x.Name == loadprofile);
            if (temp != null)
            {
                SelectedIndex = profileListHolder.ProfileListCollection.IndexOf(temp);
            }
        }

        public void RequestDisconnect()
        {
            if (device.Synced && !device.Charging)
            {
                if (device.ConnectionType == ConnectionType.BT)
                {
                    //device.StopUpdate();
                    device.QueueEvent(() =>
                    {
                        device.DisconnectBT();
                    });
                }
                else if (device.ConnectionType == ConnectionType.SONYWA)
                {
                    device.DisconnectDongle();
                }
            }
        }
    }
}
