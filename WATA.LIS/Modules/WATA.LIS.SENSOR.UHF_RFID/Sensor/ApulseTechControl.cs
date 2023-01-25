using Apulsetech.Barcode;
using Apulsetech.Remote.Type;
using Apulsetech.Remote;
using Apulsetech.Rfid;
using Apulsetech.Type;
using NetMQ;
using NetMQ.Sockets;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.RFID;
using Apulsetech.Event;
using Apulsetech.Barcode.Type;
using System.Windows.Controls;
using Msg = Apulsetech.Remote.Type.Msg;
using System.Windows;
using Apulsetech.Remote.Thread;
using Apulsetech.Rfid.Type;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class ApulseTechControl : ScannerEventListener, ReaderEventListener
    {
        private readonly IEventAggregator _eventAggregator;


        private RemoteDeviceScanner mRemoteDeviceScanner;
        private readonly MsgEvent mMsgEvent = new MsgEvent();
        private DispatcherTimer mScanTimeoutTimer;
        private DispatcherTimer mConnectionTimer;
        private int mTimeout = 30000;
        private int mSelectedRemoteDeviceIndex = -1;
        private RemoteDevice mRemoteDevice;
        private Scanner mScanner;
        private Reader mReader;
        private bool mConnected = false;
        private bool mBarcodeScanStarted = false;
        private bool mRfidInventoryStarted = false;



        public ApulseTechControl(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public void Init()
        {
            mRemoteDeviceScanner = new RemoteDeviceScanner(mMsgEvent);
            mRemoteDeviceScanner.Timeout = mTimeout;
            mRemoteDeviceScanner.ScanEnabled = true;
            mMsgEvent.msgEvent += new MsgEvent.MsgEventDelegate(HandleEvent);
            mScanTimeoutTimer = new DispatcherTimer();
            mScanTimeoutTimer.Interval = new TimeSpan(0, 0, 0, 0, mTimeout);
            mScanTimeoutTimer.Tick += new EventHandler(ScanTimeoutTimerTask);
            mScanTimeoutTimer.Start();

            mConnectionTimer = new DispatcherTimer();
            mConnectionTimer.Interval = new TimeSpan(0, 0, 0, 0, 3000);
            mConnectionTimer.Tick += new EventHandler(TestTimer);
     

            if (mRemoteDeviceScanner.Started)
            {
                StopScanTimeoutTimer();
                mRemoteDeviceScanner.ScanRemoteDevice(false);
            }
            else
            {
                mRemoteDeviceScanner.ScanRemoteDevice(true);
                StartScanTimeoutTimer();
            }
        }

        private void ToggleRfidInventory()
        {
            if (mReader != null)
            {
                if (mRfidInventoryStarted)
                {
                    if (mReader.StopOperation() == RfidResult.SUCCESS)
                    {
                        mRfidInventoryStarted = false;
                    }
                }
                else
                {
                    if (mReader.StartInventory() == RfidResult.SUCCESS)
                    {
                        mRfidInventoryStarted = true;
                    }
                }
            }
        }


        private  void TryConnect()
        {
            try
            {

                if (mRemoteDevice != null)
                {
                    
                    if (mRemoteDevice.RfidSupported)
                    {
                        mReader = Reader.GetReader(mRemoteDevice, false, 10000);

                        //mReader = await Reader.GetReaderAsync(mRemoteDevice, false, 10000);
                    }
                    if (mReader != null)
                    {
                        mConnected = true;

                        
                        if (mReader != null)
                        {
                            try
                            {
                                
                                mReader.SetEventListener(this);
                                Tools.Log("Connect Start", Tools.ELogType.RFIDLog);

                                //if (await mReader.StartAsync())
                                if (mReader.Start())

                                {
                                    Tools.Log("Start", Tools.ELogType.RFIDLog);
                                }
                                
                            }
                            catch
                            {
                                Tools.Log("Excepiton Connect Device!!!", Tools.ELogType.RFIDLog);

                            }

                            Tools.Log("Success Connect Device!!!", Tools.ELogType.RFIDLog);

                            mConnectionTimer.Stop();

                            Tools.Log("Stop Connect Timer", Tools.ELogType.RFIDLog);
                        }
                    }
                    else
                    {
                        Tools.Log("Failed to get the instance of scanner and reader device!", Tools.ELogType.RFIDLog);
                    }
                }
                else
                {
                    Tools.Log("Failed to get remote device instance!", Tools.ELogType.RFIDLog);
                }
            }
            catch
            {
                     Tools.Log("Exception Connect!!!", Tools.ELogType.RFIDLog);
            }
        }

        private void Disconnect()
        {
            if (mConnected)
            {
                if (mScanner != null)
                {
                    mScanner.RemoveEventListener(this);
                    if (mScanner.Stop())
                    {
                        mScanner.Destroy();
                        mScanner = null;
                    }
                }

                if (mReader != null)
                {
                    mReader.RemoveEventListener(this);
                    if (mReader.Stop())
                    {
                        mReader.Destroy();
                        mReader = null;
                    }
                }

                mConnected = false;
            }
        }





        private void StartScanTimeoutTimer()
        {
            if (mScanTimeoutTimer.IsEnabled)
            {
                mScanTimeoutTimer.Stop();
            }

            mScanTimeoutTimer.Interval = new TimeSpan(0, 0, 0, 0, mTimeout);
            mScanTimeoutTimer.Start();
        }

        private void StopScanTimeoutTimer()
        {
            if (mScanTimeoutTimer.IsEnabled)
            {
                mScanTimeoutTimer.Stop();
            }
        }

        private void TestTimer(object sender, EventArgs e)
        {
            StopScanTimeoutTimer();

            ConnectSequence();

        }

        private void ScanTimeoutTimerTask(object sender, EventArgs e)
        {
            StopScanTimeoutTimer();

            if (mRemoteDeviceScanner.Started)
            {
                mRemoteDeviceScanner.ScanRemoteDevice(false);
            }
        }


        private void ConnectSequence()
        {
            
            Thread.Sleep(1000);

            if (mRemoteDeviceScanner.Started)
            {
                mRemoteDeviceScanner.ScanRemoteDevice(false);
            }

            TryConnect();

            Thread.Sleep(1000);

            RemoteController.Controller?.SetRemoteDeviceTriggerActiveModule(Module.RFID);

            Thread.Sleep(1000);

            ToggleRfidInventory();
        }


        public void HandleEvent(object sender, MsgEventArg e)
        {
            if (e == null)
            {
                return;
            }

            switch (e.Arg1)
            {
                case Msg.SERIAL_ADD_DEVICE:
                case Msg.USB_ADD_DEVICE:
                case Msg.BLE_ADD_DEVICE:
                case Msg.BT_SPP_ADD_DEVICE:
                case Msg.WIFI_ADD_DEVICE:
                case Msg.WIFI_P2P_ADD_DEVICE:
                case Msg.E2S_ADD_DEVICE:
                    {
                        RemoteDevice device = (RemoteDevice)e.Arg4;
                        mRemoteDevice = device;
                    }
                    break;

                case Msg.SERIAL_DELETE_DEVICE:
                case Msg.USB_DELETE_DEVICE:
                case Msg.BLE_DELETE_DEVICE:
                case Msg.BT_SPP_DELETE_DEVICE:
                case Msg.WIFI_DELETE_DEVICE:
                case Msg.WIFI_P2P_DELETE_DEVICE:
                case Msg.E2S_DELETE_DEVICE:
                    string deviceId = (string)e.Arg4;
                    
                    break;

                case Msg.SERIAL_DEVICE_INFO_RECEIVED:
                case Msg.USB_DEVICE_INFO_RECEIVED:
                case Msg.BLE_DEVICE_INFO_RECEIVED:
                case Msg.BT_SPP_DEVICE_INFO_RECEIVED:
                case Msg.WIFI_P2P_DEVICE_INFO_RECEIVED:
                case Msg.WIFI_DEVICE_INFO_RECEIVED:
                case Msg.E2S_DEVICE_INFO_RECEIVED:
                    {
                        RemoteDevice.Detail detail = (RemoteDevice.Detail)e.Arg4;
                        mRemoteDevice.DeviceDetail = detail;
                        mConnectionTimer.Start();
                        
                    }
                    break;
            }
        }




        public void OnScannerDeviceStateChanged(DeviceEvent state)
        {
           // throw new NotImplementedException();
        }

        public void OnScannerEvent(BarcodeType type, string barcode)
        {
            //throw new NotImplementedException();
        }

        public void OnScannerRemoteKeyEvent(int action, int keyCode)
        {
            //throw new NotImplementedException();
        }

        public void OnScannerRemoteSettingChanged(int type, object value)
        {
            //throw new NotImplementedException();
        }

        public void OnReaderDeviceStateChanged(DeviceEvent state)
        {
            switch (state)
            {
                case DeviceEvent.DISCONNECTED:
                    mReader = null;
                    mRemoteDevice = null;
                    mConnected = false;

                    
                    break;
            }
        }

        public void OnReaderEvent(int eventId, int result, string data)
        {
            switch (eventId)
            {
                case Reader.READER_CALLBACK_EVENT_INVENTORY:
                    if (result == RfidResult.SUCCESS)
                    {
                        if (data != null)
                        {
                              ProcessRfidTagData(data);
                        }
                    }
                    break;

                case Reader.READER_CALLBACK_EVENT_START_INVENTORY:
                    if (!mRfidInventoryStarted)
                    {
                        mRfidInventoryStarted = true;
                                            }
                    break;

                case Reader.READER_CALLBACK_EVENT_STOP_INVENTORY:
                    if (mRfidInventoryStarted)
                    {
                        mRfidInventoryStarted = false;
                        
                    }
                    break;
            }
        }

        private void ProcessRfidTagData(string data)
        {
            string epc = "";
            string rssi = "";
            string phase = "";
            string phaseDegree = "";
            string fastID = "";
            string channel = "";
            string port = "";

            string[] dataItems = data.Split(';');
            foreach (string dataItem in dataItems)
            {
                if (dataItem.Contains("rssi"))
                {
                    int point = dataItem.IndexOf(':') + 1;
                    rssi = dataItem.Substring(point, dataItem.Length - point);
                }
                else if (dataItem.Contains("phase"))
                {
                    int point = dataItem.IndexOf(':') + 1;
                    phase = dataItem.Substring(point, dataItem.Length - point);
                    phaseDegree = phase + "˚";
                }
                else if (dataItem.Contains("fastID"))
                {
                    int point = dataItem.IndexOf(':') + 1;
                    fastID = dataItem.Substring(point, dataItem.Length - point);
                }
                else if (dataItem.Contains("channel"))
                {
                    int point = dataItem.IndexOf(':') + 1;
                    channel = dataItem.Substring(point, dataItem.Length - point);
                }
                else if (dataItem.Contains("antenna"))
                {
                    int point = dataItem.IndexOf(':') + 1;
                    port = dataItem.Substring(point, dataItem.Length - point);
                }
                else
                {
                    epc = dataItem;
                }
            }

            string[] items = new string[2];
            items[0] = epc;
            items[1] = rssi;

            Tools.Log($"RSSI {rssi}", Tools.ELogType.RFIDLog);
            Thread.Sleep(1000);


        }



        public void OnReaderRemoteKeyEvent(int action, int keyCode)
        {
            //throw new NotImplementedException();
        }

        public void OnReaderRemoteSettingChanged(int type, object value)
        {
            //throw new NotImplementedException();
        }
    }
}
