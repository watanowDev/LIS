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
using WATA.LIS.Core.Model.RFID;
using System.IO.Ports;
using WATA.LIS.Core.Model.VISION;
using ThingMagic;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class Keonn_2ch
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IRFIDModel _rfidmodel;

        SerialPort serial = new SerialPort();
        SerialPort _port = new SerialPort();

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
            const int sleepTime = 10000;  //miliseconds

            try
            {

                /* 
                 * The using statement calls the Dispose method on the object and it also causes the object to go out 
                 * of scope as soon as Dispose is called. 
                 */
                using (Reader reader = Reader.Create(URI))
                {
                    reader.Connect();

                    //The region of operation should be set
                    Reader.Region[] readerSupportedRegions = (Reader.Region[])reader.ParamGet("/reader/region/supportedRegions");
                    if (readerSupportedRegions.Length < 1)
                        throw new FAULT_INVALID_REGION_Exception();

                    //set region to EU3
                    reader.ParamSet("/reader/region/id", readerSupportedRegions[5]);

                    int[] antennaList = { 1 };

                    // uncomment the following line to check detected antennas 
                    //int[] antennaList = (int[])reader.ParamGet("/reader/antenna/portList");

                    //use the antenna #1 by default, use GEN2 protocol, don't use any filter
                    SimpleReadPlan plan = new SimpleReadPlan(antennaList, TagProtocol.GEN2, null, null, 1000);
                    reader.ParamSet("/reader/read/plan", plan);

                    //tag listener
                    reader.TagRead += delegate (Object sender, TagReadDataEventArgs e)
                    {
                        Console.WriteLine("Reading: " + e.TagReadData);
                    };

                    //exception listener
                    reader.ReadException += new EventHandler<ReaderExceptionEventArgs>(RException);

                    //read asyncronously
                    reader.StartReading();

                    Thread.Sleep(sleepTime);

                    //reads are repeated until the stopReading() method is called.
                    reader.StopReading();

                    //release resources that the API has acquired (serial device, network connection...)
                    reader.Destroy();

                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        private void CheckConnTimer(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void GetInventoryTimer(object sender, EventArgs e)
        {
            if (_port == null || _port.IsOpen == false)
            {
                SysAlarm.AddErrorCodes(SysAlarm.RFIDConnErr);
                return;
            }

            try
            {
                int bytesize = _port.BytesToRead;
                byte[] buffer = new byte[bytesize];
                _port.Read(buffer, 0, bytesize);
                if (bytesize >= 25)
                {

                    LogRawData(buffer);
                    ParseData(buffer, bytesize);
                    SysAlarm.RemoveErrorCodes(SysAlarm.RFIDConnErr);
                }
            }
            catch
            {
                Tools.Log($"[DataRecive] Exception !!!", Tools.ELogType.RFIDLog);
                SysAlarm.AddErrorCodes(SysAlarm.RFIDConnErr);
            }
        }

        public void ParseData(byte[] RecvBytes, int nSize)
        {
            if (RecvBytes[0] == 0x55 && RecvBytes[1] == 0xAB && RecvBytes[2] == 0x01)
            {

                byte[] EPC = new byte[4];
                System.Buffer.BlockCopy(RecvBytes, 4, EPC, 0, 4);
                Array.Reverse(EPC);


                byte[] TimeStamp = new byte[4];
                System.Buffer.BlockCopy(RecvBytes, 8, TimeStamp, 0, 4);
                Array.Reverse(TimeStamp);


                byte[] RSSI = new byte[4];
                System.Buffer.BlockCopy(RecvBytes, 12, RSSI, 0, 4);
                Array.Reverse(RSSI);



                string strEPC = BitConverter.ToInt16(EPC, 0);
                DateTime Timestamp = BitConverter.ToInt16(TimeStamp, 0);
                int nRSSI = BitConverter.ToInt16(RSSI, 0);

                int right_forkpower = RecvBytes[13];
                int left_forkpower = RecvBytes[16];

                Keonn4chSensorModel model = new Keonn4chSensorModel();
                model.GrossRFID = strEPC;
                model.RightRFID = nRightRFID;
                model.LeftRFID = nRSSI;

                Tools.Log($"nGrossRFID : {strEPC}", Tools.ELogType.RFIDLog);
                Tools.Log($"nRightRFID : {nRightRFID}", Tools.ELogType.RFIDLog);
                Tools.Log($"nLeftRFID : {nRSSI}", Tools.ELogType.RFIDLog);
                Tools.Log($"right_forkpower : {right_forkpower}", Tools.ELogType.RFIDLog);
                Tools.Log($"left_forkpower : {left_forkpower}", Tools.ELogType.RFIDLog);

                Tools.Log($"[DataRecive] {strEPC} ", Tools.ELogType.RFIDLog);
                _eventAggregator.GetEvent<RFIDSensorEvent>().Publish(model);
            }


            return;
        }

        private void LogRawData(byte[] HexData)
        {
            String strData = "";
            for (int i = 0; i < HexData.Length; i++)
            {
                strData += String.Format("0x{0:x2} ", HexData[i]);
            }
            Tools.Log($"LEN : {HexData.Length} RAW : {strData}", Tools.ELogType.RFIDLog);
        }
    }
}
