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
        private readonly IWeightModel _weightmodel;

        private WeightConfigModel _weightConfig;
        private SerialPort _port = new SerialPort();

        private DispatcherTimer m_receiveTimer;
        private DispatcherTimer m_newVerReceiveTimer;
        private DispatcherTimer m_checkConnectionTimer;
        private int m_nDataSize = 0;

        private bool log_enable = true;

        public LatchLoadCell(IEventAggregator eventAggregator, IWeightModel weightmodel)
        {
            _eventAggregator = eventAggregator;
            _weightmodel = weightmodel;
            _weightConfig = (WeightConfigModel)_weightmodel;
        }

        public void Init()
        {
            //m_receiveTimer = new DispatcherTimer();
            //m_receiveTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            //m_receiveTimer.Tick += new EventHandler(ReceiveTimerEvent);

            //SerialThreadInit();

            m_newVerReceiveTimer = new DispatcherTimer();
            m_newVerReceiveTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_newVerReceiveTimer.Tick += new EventHandler(NewVerReceiveTimerEvent);

            SerialThreadInit_NewVersion();

            //m_checkConnectionTimer = new DispatcherTimer();
            //m_checkConnectionTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //m_checkConnectionTimer.Tick += new EventHandler(CheckConnectionEvent);
            //m_checkConnectionTimer.Start();

            _eventAggregator.GetEvent<WeightSensorSendEvent>().Subscribe(onSendData, ThreadOption.BackgroundThread, true);
        }

        private void SerialThreadInit_NewVersion()
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
                    m_newVerReceiveTimer.Start();
                    Tools.Log($"Weight Sensor Init Success", Tools.ELogType.SystemLog);
                    SysAlarm.RemoveErrorCodes(SysAlarm.WeightConnErr);
                }

                if (_port == null || !_port.IsOpen)
                {
                    Tools.Log($"Weight Port is not open", Tools.ELogType.SystemLog);
                    return;
                }
            }
            catch (Exception ex)
            {
                _port = null;
                m_receiveTimer.Stop();
                Tools.Log($"Weight Port Exception !!!", Tools.ELogType.SystemLog);
                SysAlarm.AddErrorCodes(SysAlarm.WeightConnErr);
            }
        }

        private void NewVerReceiveTimerEvent(object sender, EventArgs e)
        {
            // 전송할 데이터
            byte[] dataToSend = new byte[] { 0x55, 0xAB, 0x01, 0x00 };
            _port.Write(dataToSend, 0, dataToSend.Length);

            // 응답 데이터 수신
            Thread.Sleep(100); // 잠시 대기하여 데이터 수신
            int bytesToRead = _port.BytesToRead;
            if (bytesToRead >= 25)
            {
                byte[] receivedData = new byte[bytesToRead];
                _port.Read(receivedData, 0, bytesToRead);

                // 응답 데이터를 publish
                ParseData(receivedData, receivedData.Length);
            }
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
                    m_receiveTimer.Start();
                    Tools.Log($"Weight Sensor Init Success", Tools.ELogType.SystemLog);
                    SysAlarm.RemoveErrorCodes(SysAlarm.WeightConnErr);
                }

                if (_port == null || !_port.IsOpen)
                {
                    Tools.Log($"Weight Port is not open", Tools.ELogType.SystemLog);
                    return;
                }
            }
            catch
            {
                _port = null;
                m_receiveTimer.Stop();
                Tools.Log($"Weight Port Exception !!!", Tools.ELogType.SystemLog);
                SysAlarm.AddErrorCodes(SysAlarm.WeightConnErr);
            }
        }

        private void ReceiveTimerEvent(object sender, EventArgs e)
        {
            if (_port == null || _port.IsOpen == false)
            {
                SysAlarm.AddErrorCodes(SysAlarm.WeightConnErr);
                return;
            }

            try
            {
                m_nDataSize = _port.BytesToRead;
                byte[] buffer = new byte[m_nDataSize];
                _port.Read(buffer, 0, m_nDataSize);
                if (m_nDataSize >= 25)
                {

                    //LogRawData(buffer);
                    ParseData(buffer, m_nDataSize);
                    SysAlarm.RemoveErrorCodes(SysAlarm.WeightConnErr);
                }
            }
            catch
            {
                Tools.Log($"[DataRecive] Exception !!!", Tools.ELogType.WeightLog);
                SysAlarm.AddErrorCodes(SysAlarm.WeightConnErr);
            }
        }

        private void CheckConnectionEvent(object sender, EventArgs e)
        {
            if (_port == null || _port.IsOpen == false || m_nDataSize < 25)
            {
                if (_port != null)
                {
                    _port.Close();
                    _port.Dispose();
                    _port = null;
                }

                //SerialThreadInit();
            }
        }

        public void ParseData(byte[] RecvBytes, int nSize)
        {
            try
            {
                if (RecvBytes[0] == 0x55 && RecvBytes[1] == 0xAB && RecvBytes[2] == 0x01)
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


                    int right_battery = RecvBytes[16];
                    int right_charge_status = RecvBytes[17];
                    int right_online_status = RecvBytes[18];


                    int left_battery = RecvBytes[19];
                    int left_charge_status = RecvBytes[20];
                    int left_online_status = RecvBytes[21];

                    int gross_net = RecvBytes[22];
                    int overload = RecvBytes[23];
                    int out_of_tolerance = RecvBytes[24];


                    WeightSensorModel model = new WeightSensorModel();
                    model.GrossWeight = nGrossWeight;
                    model.RightWeight = nRightWeight;
                    model.LeftWeight = nLeftWeight;
                    model.RightBattery = right_battery;
                    model.LeftBattery = left_battery;
                    model.RightIsCharging = right_charge_status == 1 ? true : false;
                    model.leftIsCharging = left_charge_status == 1 ? true : false;
                    model.RightOnline = right_online_status == 0 ? true : false;
                    model.LeftOnline = left_online_status == 0 ? true : false;
                    model.GrossNet = gross_net == 1 ? true : false;
                    model.OverLoad = overload == 1 ? true : false;
                    model.OutOfTolerance = out_of_tolerance == 0 ? false : true;


                    _eventAggregator.GetEvent<WeightSensorEvent>().Publish(model);
                }
            }
            catch
            {
                Tools.Log($"Weight ParseData Exception !!!", Tools.ELogType.SystemLog);
            }
        }

        private void onSendData(byte[] buffer)
        {
            try
            {
                // 수신된 데이터를 로그에 기록
                LogRawData(buffer);
            }
            catch (Exception ex)
            {
                Tools.Log($"[onSendData] Exception: {ex.Message}", Tools.ELogType.WeightLog);
            }
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

        //1.The data transmission order is to send high bytes first																	
        //2.Length field: The number of bytes of data following this length field																	
        //3.The battery is a percentage of 0-100;																	
        //4.Charging state：0:Uncharged  1:Charging																	
        //5.Online status：0:Online；1：Offline；2：Hardware failure																	
        //6.Gross and net weight mark：0:Gross weight；1：Net weight																	
        //7.Overload mark：0:Not overloaded；1：Overload																	
        //8.Out of tolerance mark：0:Not out of tolerance；1：Left out of tolerance   2：Right out of tolerance																	
        //9.Communication interface：RS485, 9600， 8N1;																	
        //10.Return status: 0 indicates normal; Non 0 is an exception
    }
}
