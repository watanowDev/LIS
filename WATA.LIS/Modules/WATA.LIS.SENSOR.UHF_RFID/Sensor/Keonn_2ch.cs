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
using Newtonsoft.Json;
using WATA.LIS.Core.Model.BackEnd;
using Windows.UI.WindowManagement;
using WATA.LIS.Core.Model.RIFID;
using System.IO.Ports;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class Keonn_2ch
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IRFIDModel _rfidmodel;

        RFIDConfigModel rfidConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mGetInventoryTimer;
        private bool mConnected = false;
        //private string mDeviceID;


        public Keonn_2ch(IEventAggregator eventAggregator, IRFIDModel rfidmodel, IMainModel main)
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

            mCheckConnTimer = new DispatcherTimer();
            mCheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            mCheckConnTimer.Tick += new EventHandler(CheckConnTimer);

            mGetInventoryTimer = new DispatcherTimer();
            mGetInventoryTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            mGetInventoryTimer.Tick += new EventHandler(GetInventoryTimer);

            RfidReaderInit();
        }

        private void RfidReaderInit()
        {
            //try
            //{
            //    string port = rfidConfig.comport;
            //    int bouadrate = 115200;
            //    _port = new SerialPort(port, bouadrate, Parity.None, 8, StopBits.One);
            //    if (_port != null)
            //    {
            //        _port.Open();
            //        _port.Handshake = Handshake.None;
            //        // _port.DataReceived += new SerialDataReceivedEventHandler(DataRecive);
            //        Tools.Log($"Init Success", Tools.ELogType.WeightLog);
            //        SysAlarm.RemoveErrorCodes(SysAlarm.WeightConnErr);
            //    }



            //}
            //catch
            //{
            //    _port = null;
            //    Tools.Log($"Serial Port Exception !!!", Tools.ELogType.WeightLog);
            //    SysAlarm.AddErrorCodes(SysAlarm.WeightConnErr);
            //}
        }

        private void CheckConnTimer(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void GetInventoryTimer(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
