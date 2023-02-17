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
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using java.lang;
using Thread = System.Threading.Thread;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class ApulseTechControl : ScannerEventListener, ReaderEventListener
    {
        private readonly IEventAggregator _eventAggregator;
        private RemoteDeviceScanner mRemoteDeviceScanner;
        private readonly MsgEvent mMsgEvent = new MsgEvent();
        private DispatcherTimer mScanTimeoutTimer;
        private DispatcherTimer mConnectionTimer;
        private DispatcherTimer mStatusCheckTimer;

        private int mTimeout = 30000;
        private RemoteDevice mRemoteDevice;
        private Scanner mScanner;
        private Reader mReader;
        private bool mConnected = false;
        private bool mRfidInventoryStarted = false;
        private readonly IRFIDModel _rfidmodel;

        RFIDConfigModel rfidConfig;
        private  string TargetMAC = "00:05:C4:C1:01:33";

        private eDeviceType devicetype = eDeviceType.ForkLift_V2;


        public ApulseTechControl(IEventAggregator eventAggregator, IRFIDModel rfidmodel, IMainModel main)
        {
            _eventAggregator = eventAggregator;
            _rfidmodel = rfidmodel;
            rfidConfig = (RFIDConfigModel)_rfidmodel;
            TargetMAC = rfidConfig.SPP_MAC;//Load Json System Config

            MainConfigModel main_config = (MainConfigModel)main;

            if (main_config.device_type == "gate_checker")
            {
                devicetype = eDeviceType.GateChecker;

            }
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

         
            mConnectionTimer = new DispatcherTimer();
            mConnectionTimer.Interval = new TimeSpan(0, 0, 0, 0, 3000);
            mConnectionTimer.Tick += new EventHandler(ConnectTimer);


            mStatusCheckTimer = new DispatcherTimer();
            mStatusCheckTimer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            mStatusCheckTimer.Tick += new EventHandler(StatusCheckTimer);
            mStatusCheckTimer.Start();



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
            Tools.Log("Start Toggle Inventory", Tools.ELogType.RFIDLog);


            if (mReader != null)
            {
                if (mRfidInventoryStarted)
                {
                    if (mReader.StopOperation() == RfidResult.SUCCESS)
                    {
                        mRfidInventoryStarted = false;
                        Tools.Log("StopOperation Toggle Inventory", Tools.ELogType.RFIDLog);
                    }
                }
                else
                {
                    if (mReader.StartInventory() == RfidResult.SUCCESS)
                    {
                        mRfidInventoryStarted = true;
                        Tools.Log("StartInventory Toggle Inventory", Tools.ELogType.RFIDLog);
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

        private void ConnectTimer(object sender, EventArgs e)
        {
            StopScanTimeoutTimer();

            ConnectSequence();

        }

        private void StatusCheckTimer(object sender, EventArgs e)
        {
            if (mConnected)
            {
                // Tools.Log($"Is Connect True", Tools.ELogType);
             
            }
            else
            {
                if (mRemoteDeviceScanner.Started)
                {
                   // Tools.Log($"Scan Stop", Tools.ELogType.BackEndLog);
                    StopScanTimeoutTimer();
                    mRemoteDeviceScanner.ScanRemoteDevice(false);
                }
                else
                {
                  //  Tools.Log($"Scan Start", Tools.ELogType.BackEndLog);
                    mRemoteDeviceScanner.ScanRemoteDevice(true);
                    StartScanTimeoutTimer();
                }
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


        private void ConnectSequence()
        {
            
            Thread.Sleep(500);
            if (mRemoteDeviceScanner.Started)
            {
                mRemoteDeviceScanner.ScanRemoteDevice(false);  
            }
            TryConnect();
            Thread.Sleep(500);

            if (rfidConfig.nSpeakerEnable == 0)
            {
                RemoteController.Controller?.ControlRemoteDeviceVibrator(false, 1, 2, 100);
                RemoteController.Controller?.SetRemoteDeviceSoundState(false);// Sound ON/OFF
                RemoteController.Controller?.SetRemoteDeviceBootSoundState(false);
                RemoteController.Controller?.SetRemoteDeviceSoundVolume(0);

            }
            else
            {
                RemoteController.Controller?.ControlRemoteDeviceVibrator(true, 1, 2, 100);
                RemoteController.Controller?.SetRemoteDeviceSoundState(true);// Sound ON/OFF
                RemoteController.Controller?.SetRemoteDeviceBootSoundState(true);
                RemoteController.Controller?.SetRemoteDeviceSoundVolume(50);

            }

            RemoteController.Controller?.SetRemoteDeviceTriggerActiveModule(Module.RFID);
            Thread.Sleep(500);



            mReader.SetInventoryAntennaPortReportState(1);

            if(rfidConfig.nRadioPower > 30)
            {
                rfidConfig.nRadioPower = 30;
            }

            if (rfidConfig.nRadioPower < 0)
            {
                rfidConfig.nRadioPower = 5;
            }

            mReader.SetRadioPower(rfidConfig.nRadioPower);
            mReader.SetTxOnTime(rfidConfig.nTxOnTime); //100,300
            mReader.SetTxOffTime(rfidConfig.nTxOffTime);
            mReader.SetToggle(rfidConfig.nToggle);

            Tools.Log($"Config nTxOffTime [{rfidConfig.nTxOffTime}] nTxOnTime [{rfidConfig.nTxOnTime}] nToggle [{rfidConfig.nToggle}] nRadioPower [{rfidConfig.nRadioPower}]", Tools.ELogType.RFIDLog);

            ToggleRfidInventory();
        }
      
        //private const string TargetMAC = "00:05:C4:C1:01:32";
        //private const string TargetMAC = "00:05:C1:C1:00:01";
        //private const string TargetMAC = "00:05:C4:C1:01:2C";



        public void HandleEvent(object sender, MsgEventArg e)
        {
            string ConnectMAC = "";


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
                        ConnectMAC = device.Address.ToUpper();
                        Tools.Log($"Scan BlueTooth SPP MAC .. {ConnectMAC}", Tools.ELogType.RFIDLog);
                        if (ConnectMAC == TargetMAC)
                        {
                            mRemoteDevice = device;
                            Tools.Log($"Try Connect Bluetooth MAC .. {ConnectMAC}", Tools.ELogType.RFIDLog);
                        }
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
                        
                        if(mRemoteDevice != null && detail.RfidStatus == RemoteStatus.STATE_IDLE)
                        {
                            Tools.Log($"Connect Start", Tools.ELogType.RFIDLog);
                            mRemoteDevice.DeviceDetail = detail;
                            mConnectionTimer.Start();
                        }
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
                    mRfidInventoryStarted = false; 


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
          //  try
            {
                string epc = "";
                string rssi = "";
                string phase = "";
                string phaseDegree = "";
                string fastID = "";
                string channel = "";
                string port = "";
                // Tools.Log($"data : {data}", Tools.ELogType.RFIDLog);



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

                if(epc.Length > 28)
                {
                    Tools.Log($"length is short", Tools.ELogType.RFIDLog);

                    return ;
                }


                string SendEPC = epc.Substring(4, 24);

                Tools.Log($"epc {SendEPC} RSSI {rssi} phaseDegree {phaseDegree} fastID {fastID} channel {channel} port {port}", Tools.ELogType.RFIDLog);


                string stx = SendEPC.Substring(0, 2);
                string etx = SendEPC.Substring(22, 2);

                
                if (SendEPC.Length == 24 && stx == "DA" && etx == "ED")
                {



                }
                else
                {

                    Tools.Log($"Format Missing EPC Data", Tools.ELogType.RFIDLog);
                    return;
                }
                
                if (devicetype == eDeviceType.GateChecker)// 게이트 감지기
                {
                    GateRFIDEventModel gate = new GateRFIDEventModel();
                    gate.GateValue = port;
                    gate.EPC = SendEPC;
                    gate.RSSI = Float.parseFloat(rssi);
                    _eventAggregator.GetEvent<Gate_Event>().Publish(gate);
                }
                else //지게차 
                {
                    if (port == "0") // 측면(측위용) 안테나 안테나
                    {
                        LocationRFIDEventModel location = new LocationRFIDEventModel();
                        location.EPC = SendEPC;
                        location.RSSI = Float.parseFloat(rssi);
                        _eventAggregator.GetEvent<LocationProcess_Event>().Publish(location);
                    }
                    else //정면(선반용)  안테나
                    {

                        RackRFIDEventModel rfidmodel = new RackRFIDEventModel();
                        rfidmodel.EPC = SendEPC;
                        rfidmodel.RSSI = Float.parseFloat(rssi);
                        _eventAggregator.GetEvent<RackProcess_Event>().Publish(rfidmodel);
                    }
                }
            }
          //  catch
            {


            }

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
