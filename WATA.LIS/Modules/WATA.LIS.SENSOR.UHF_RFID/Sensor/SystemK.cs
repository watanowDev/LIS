using Apulsetech.Remote.Type;
using Apulsetech.Remote;
using Apulsetech.Type;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using Apulsetech.Remote.Thread;
using System.Threading;
using Apulsetech.Rfid.Type;
using System.Net;
using System.IO;
using sun.misc;
using System.Net.NetworkInformation;
using System.Xml;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net.Http;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class SystemK
    {
        private readonly IEventAggregator _eventAggregator;
        private RemoteDeviceScanner mRemoteDeviceScanner;
        private readonly MsgEvent mMsgEvent = new MsgEvent();
        private DispatcherTimer mScanTimeoutTimer;
        private DispatcherTimer mConnectionTimer;
        private DispatcherTimer mStatusCheckTimer;

        private int mTimeout = 30000;
        private RemoteDevice mRemoteDevice;
        private bool mConnected = false;
        private bool mRfidInventoryStarted = false;
        private readonly IRFIDModel _rfidmodel;

        RFIDConfigModel rfidConfig;

        private String rfidUri;


        public SystemK(IEventAggregator eventAggregator, IRFIDModel rfidmodel, IMainModel main)
        {
            _eventAggregator = eventAggregator;
            _rfidmodel = rfidmodel;
            rfidConfig = (RFIDConfigModel)_rfidmodel;

            MainConfigModel main_config = (MainConfigModel)main;
        }

        public void Init()
        {
            if (rfidConfig.rfid_enable == 0)
            {
                Tools.Log("rftag disable", Tools.ELogType.RFIDLog);
                return;
            }

            mRemoteDeviceScanner = new RemoteDeviceScanner(mMsgEvent);
            mRemoteDeviceScanner.Timeout = mTimeout;
            mRemoteDeviceScanner.ScanEnabled = true;
            mMsgEvent.msgEvent += new MsgEvent.MsgEventDelegate(HandleEvent);
            mScanTimeoutTimer = new DispatcherTimer();
            mScanTimeoutTimer.Interval = new TimeSpan(0, 0, 0, 0, mTimeout);
            mScanTimeoutTimer.Tick += new EventHandler(ScanTimeoutTimerTask);

            mConnectionTimer = new DispatcherTimer();
            mConnectionTimer.Interval = new TimeSpan(0, 0, 0, 0, 3000);
            mConnectionTimer.Tick += new EventHandler(ConnectTimer);
        }

        public void HandleEvent(object sender, MsgEventArg e)
        {
            if (e == null)
            {
                return;
            }

            string connectIP;
            RemoteDevice device = (RemoteDevice)e.Arg4;
            connectIP = device.Address.ToString();
            Tools.Log($"Scan IP .. {connectIP}", Tools.ELogType.RFIDLog);

            if (connectIP == rfidConfig.ip)
            {
                Tools.Log("Try Connect Front RFID", Tools.ELogType.RFIDLog);
            }
        }

        private void ScanTimeoutTimerTask(object sender, EventArgs e)
        {
            StopScanTimeoutTimer();

            if (mRemoteDeviceScanner.Started)
            {
                mRemoteDeviceScanner.ScanRemoteDevice(false);
            }
        }

        private void ConnectTimer(object sender, EventArgs e)
        {
            StopScanTimeoutTimer();

            ConnectSequence();
        }

        private void StopScanTimeoutTimer()
        {
            if (mScanTimeoutTimer.IsEnabled)
            {
                mScanTimeoutTimer.Stop();
            }
        }

        private void ConnectSequence()
        {
            Thread.Sleep(500);
            if (mRemoteDeviceScanner.Started)
            {
                mRemoteDeviceScanner.ScanRemoteDevice(false);
            }
            TryConnect();
            Thread.Sleep(500);

            if (rfidConfig.nSpeakerlevel == 0)
            {
                RemoteController.Controller?.ControlRemoteDeviceVibrator(false, 1, 2, 100);
                RemoteController.Controller?.SetRemoteDeviceSoundState(false);// Sound ON/OFF
                RemoteController.Controller?.SetRemoteDeviceBootSoundState(false);
                RemoteController.Controller?.SetRemoteDeviceSoundVolume(0);
            }
            else
            {
                RemoteController.Controller?.ControlRemoteDeviceVibrator(false, 1, 2, 100);
                RemoteController.Controller?.SetRemoteDeviceSoundState(true);// Sound ON/OFF
                RemoteController.Controller?.SetRemoteDeviceBootSoundState(true);
                RemoteController.Controller?.SetRemoteDeviceSoundVolume(rfidConfig.nSpeakerlevel);
            }

            RemoteController.Controller?.SetRemoteDeviceTriggerActiveModule(Module.RFID);
            Thread.Sleep(500);

            if (rfidConfig.nRadioPower > 30)
            {
                rfidConfig.nRadioPower = 30;
            }

            if (rfidConfig.nRadioPower < 0)
            {
                rfidConfig.nRadioPower = 5;
            }

            Tools.Log($"Config nTxOffTime [{rfidConfig.nTxOffTime}] nTxOnTime [{rfidConfig.nTxOnTime}] nToggle [{rfidConfig.nToggle}] nRadioPower [{rfidConfig.nRadioPower}]", Tools.ELogType.RFIDLog);

            ToggleRfidInventory();
        }

        private void TryConnect()
        {
            string response;
            response = GetResponseFromURI().Result;

            if (response == string.Empty)
            {
                mConnected = false;
                Tools.Log("Empty String", Tools.ELogType.RFIDLog);
            }
            else
            {
                mConnected = true;
                Tools.Log("Reader Connected", Tools.ELogType.RFIDLog);
            }
        }

        private void ToggleRfidInventory()
        {
            Tools.Log("Start Toggle Inventory", Tools.ELogType.RFIDLog);
        }

        private async Task<string> GetResponseFromURI()
        {
            string response = string.Empty;
            rfidUri = $"http://{rfidConfig.ip}:{rfidConfig.port}/devices/";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(mTimeout);
                    HttpResponseMessage httpResponse = await client.GetAsync(rfidUri);
                    response = await httpResponse.Content.ReadAsStringAsync();
                }
            }
            catch
            {
                Tools.Log($"Exception Connect!!!", Tools.ELogType.RFIDLog);
            }

            return response;
        }
    }
}
