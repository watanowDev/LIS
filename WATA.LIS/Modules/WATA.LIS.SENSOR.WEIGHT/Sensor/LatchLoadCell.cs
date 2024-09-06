using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Markup;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.VISION;
using WATA.LIS.Core.Services;
using Windows.Storage.Streams;

namespace WATA.LIS.SENSOR.WEIGHT.Sensor
{
    public class LatchLoadCell
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IWeightModel  _weightmodel;

        SerialPort serial = new SerialPort();
        SerialPort _port = new SerialPort();


        WeightConfigModel _weightConfig;


        //DistanceConfigModel _distaceConfig;

        public LatchLoadCell(IEventAggregator eventAggregator, IWeightModel weightmodel)
        {                          
            _eventAggregator = eventAggregator;
            _weightmodel = weightmodel;
            _weightConfig = (WeightConfigModel)_weightmodel;



        }

        private bool log_enable = true;


        public void SerialInit()
        {
            SerialThreadInit();
            DispatcherTimer ReceiveTimer = new DispatcherTimer();
            ReceiveTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            ReceiveTimer.Tick += new EventHandler(ReceiveTimerEvent);
            ReceiveTimer.Start();

            _eventAggregator.GetEvent<WeightSensorSendEvent>().Subscribe(onSendData, ThreadOption.BackgroundThread, true);
        }



        private void SerialThreadInit()
        {

            try
            {
                string port = _weightConfig.ComPort;
                int bouadrate = 9600;
                _port = new SerialPort(port, bouadrate, Parity.None, 8, StopBits.One);
                 if (_port != null)
                {
                    _port.Open();
                    _port.Handshake = Handshake.None;
                   // _port.DataReceived += new SerialDataReceivedEventHandler(DataRecive);
                    Tools.Log($"Init Success", Tools.ELogType.WeightLog);
                }


          
            }
            catch
            {
                _port = null;
                Tools.Log($"Serial Port Exception !!!", Tools.ELogType.WeightLog);
                Tools.AddErrorCode(Tools.EEroorCodes.WeightConnError);
            }
        }

    private void onSendData(byte[] buffer)
    {
            string bytelog = Util.DebugBytestoString(buffer);
            Tools.Log($"Send packet {bytelog}", Tools.ELogType.WeightLog);

            if (_port == null || _port.IsOpen == false)
            {

                return;
            }
            _port.Write(buffer, 0, buffer.Length);
    }


    private void ReceiveTimerEvent(object sender, EventArgs e)
    {
            if (_port == null || _port.IsOpen == false)
            {

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
                }
            }
            catch
            {
                Tools.Log($"[DataRecive] Exception !!!", Tools.ELogType.WeightLog);
            }
    }

        public void ParseData(byte[] RecvBytes, int nSize)
        {
            if (RecvBytes[0] == 0x55 && RecvBytes[1] ==  0xAB && RecvBytes[2] == 0x01)
            {

                byte[] GrossWeight = new byte[4];
                System.Buffer.BlockCopy(RecvBytes, 4, GrossWeight, 0, 4);
                Array.Reverse(GrossWeight);


                byte[] RightWeight = new byte[4];
                System.Buffer.BlockCopy(RecvBytes, 8, RightWeight, 0, 4);
                Array.Reverse(RightWeight);


                byte[] LeftWeight = new byte[4];
                System.Buffer.BlockCopy(RecvBytes, 12, LeftWeight, 0, 4);
                Array.Reverse(LeftWeight);



                int nGrossWeight = BitConverter.ToInt16(GrossWeight, 0);
                int nRightWeight = BitConverter.ToInt16(RightWeight, 0);
                int nLeftWeight = BitConverter.ToInt16(LeftWeight, 0);

                int right_forkpower = RecvBytes[13];
                int left_forkpower = RecvBytes[16];

                WeightSensorModel model = new WeightSensorModel();
                model.GrossWeight =  nGrossWeight;
                model.RightWeight = nRightWeight;
                model.LeftWeight = nLeftWeight;

                Tools.Log($"nGrossWeight : {nGrossWeight}", Tools.ELogType.WeightLog);
                Tools.Log($"nRightWeight : {nRightWeight}", Tools.ELogType.WeightLog);
                Tools.Log($"nLeftWeight : {nLeftWeight}", Tools.ELogType.WeightLog);
                Tools.Log($"right_forkpower : {right_forkpower}", Tools.ELogType.WeightLog);
                Tools.Log($"left_forkpower : {left_forkpower}", Tools.ELogType.WeightLog);

                Tools.Log($"[DataRecive] {nGrossWeight} ", Tools.ELogType.WeightLog);
                _eventAggregator.GetEvent<WeightSensorEvent>().Publish(model);
            }
           

            return ;
        }

        private void LogRawData(byte[] HexData)
        {
            if (log_enable == false)
            {
                return;
            }

            String strData = "";
            for (int i = 0; i < HexData.Length; i++)
            {
                strData += String.Format("0x{0:x2} ", HexData[i]);
            }
            Tools.Log($"LEN : {HexData.Length} RAW : {strData}", Tools.ELogType.WeightLog);
        }

        private void DataRecive(object sender, SerialDataReceivedEventArgs e) 
    {
            try
            {
                SerialPort sp = (SerialPort)sender;


                int bytesize = sp.BytesToRead;

                if (bytesize > 0)
                {
                    byte[] RecvBytes = new byte[bytesize];
                    sp.Read(RecvBytes, 0, bytesize);



                    LogRawData(RecvBytes);

                }
            }
            catch
            {
                Tools.Log($"[DataRecive] Exception !!!", Tools.ELogType.WeightLog);
            }
            Thread.Sleep(300);
        }
    }
}
