using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.IO.Ports;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events;
using WATA.LIS.Core.Model;

namespace WATA.LIS.SENSOR.Distance.Sensor
{
    public class DistanceSensor
    {
        private readonly IEventAggregator _eventAggregator;
        SerialPort serial = new SerialPort();
        public DistanceSensor(IEventAggregator eventAggregator)
        {

            _eventAggregator = eventAggregator;
            SerialInit();
        }

        private bool log_enable = true;

        private void SerialInit()
        {
            serial.PortName = "COM3";
            serial.BaudRate = 115200;
            serial.DataBits = 8;
            serial.StopBits = StopBits.One;
            serial.Parity = Parity.None;
            serial.DataReceived += new SerialDataReceivedEventHandler(DataRecive);
            serial.Open();
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
                    AverageData(RecvBytes, bytesize);

                }
            }
            catch
            {
                Tools.Log($"[DataRecive] Exception !!!", Tools.ELogType.DistanceLog);
            }
            Thread.Sleep(300);
        }

        public string AverageData(byte[] RecvBytes, int nSize)
        {
            string retStr = "";
            List<int> list_distance = new List<int>();

            for (int idx = 0; idx < nSize; idx += 4)
            {
                int stxIndex = idx;
                int bodyIndex =stxIndex + 1;
                if ((RecvBytes[stxIndex] == 0x54))
                {
                    byte[] Temp = new byte[2];
                    System.Buffer.BlockCopy(RecvBytes, bodyIndex, Temp, 0, 2);
                    Array.Reverse(Temp);
                    list_distance.Add(BitConverter.ToInt16(Temp, 0));   
                }
                else
                {
                   Tools.Log($" Parse Fail", Tools.ELogType.DistanceLog);
                }
            }

            int AverageDistance = (int)list_distance.Average();
            DistanceSensorModel DisTanceObject = new DistanceSensorModel();
            DisTanceObject.Distance_mm = AverageDistance;
            _eventAggregator.GetEvent<DistanceSensorEvent>().Publish(DisTanceObject);
          
            if (log_enable)
            {
                Tools.Log($"Distance : {AverageDistance}", Tools.ELogType.DistanceLog);
            }
            return retStr;
        }

        private void LogRawData(byte[] HexData)
        {
            if(log_enable == false)
            {
                return;
            }
               
            String strData = "";
            for (int i = 0; i < HexData.Length; i++)
            {
                strData += String.Format("0x{0:x2} ", HexData[i]);
            }
            Tools.Log($"LEN : {HexData.Length} RAW : {strData}", Tools.ELogType.DistanceLog);
        }
    }
}
