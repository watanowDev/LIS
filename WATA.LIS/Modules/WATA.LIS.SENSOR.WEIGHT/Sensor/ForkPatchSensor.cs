using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Services;

namespace WATA.LIS.SENSOR.WEIGHT.Sensor
{
    public class ForkPatchSensor
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IWeightModel  _weightmodel;

        SerialPort serial = new SerialPort();
        SerialPort _port = new SerialPort();


        WeightConfigModel _weightConfig;


        //DistanceConfigModel _distaceConfig;

        public ForkPatchSensor(IEventAggregator eventAggregator, IWeightModel weightmodel)
        {                          
            _eventAggregator = eventAggregator;
            _weightmodel = weightmodel;
            _weightConfig = (WeightConfigModel)_weightmodel;
        }

        private bool log_enable = true;


        public void SerialInit()
        {
            Tools.Log($"SerialInit Success", Tools.ELogType.WeightLog);
            SerialThreadInit();
            DispatcherTimer ReceiveTimer = new DispatcherTimer();
            ReceiveTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            ReceiveTimer.Tick += new EventHandler(ReceiveTimerEvent);
            ReceiveTimer.Start();
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
                    Tools.Log($"Init Success", Tools.ELogType.WeightLog);
                }
            }
            catch
            {
                _port = null;
                Tools.Log($"Serial Port Exception !!!", Tools.ELogType.WeightLog);
            }
        }


    private void ReceiveTimerEvent(object sender, EventArgs e)
    {
            if (_port == null || _port.IsOpen == false)
            {
                return;
            }

            try
            {
                string recv_str = _port.ReadLine();
                Tools.Log($"[DataRecive] {recv_str} ", Tools.ELogType.WeightLog);
                _eventAggregator.GetEvent<WeightSensorEvent>().Publish(recv_str);
            }
            catch
            {
                Tools.Log($"[DataRecive] Exception !!!", Tools.ELogType.WeightLog);
            }

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
