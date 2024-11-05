using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.SystemConfig;
using static sun.security.jca.GetInstance;

namespace WATA.LIS.SENSOR.Distance.Sensor
{
    public class TF_Mini
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IDistanceModel _distancemodel;

        SerialPort serial = new SerialPort();
        SerialPort _port = new SerialPort();


        DistanceConfigModel _DistanceConfig;

        public TF_Mini(IEventAggregator eventAggregator, IDistanceModel distancemodel)
        {
            _eventAggregator = eventAggregator;
            _distancemodel = distancemodel;

            _DistanceConfig = (DistanceConfigModel)_distancemodel;
        }


        public void SerialInit()
        {
            SerialThreadInit();
            DispatcherTimer ReceiveTimer = new DispatcherTimer();
            ReceiveTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            ReceiveTimer.Tick += new EventHandler(ReceiveTimerEvent);
            ReceiveTimer.Start();
        }

        private void SerialThreadInit()
        {
            try
            {
                string port = _DistanceConfig.ComPort;
                int bouadrate = 115200;
                _port = new SerialPort(port, bouadrate, Parity.None, 8, StopBits.One);
                if (_port != null)
                {
                    _port.Open();
                    _port.Handshake = Handshake.None;
                    Tools.Log($"Distance Init Success", Tools.ELogType.SystemLog);
                    SysAlarm.RemoveErrorCodes(SysAlarm.DistanceConnErr);
                }
            }
            catch
            {
                _port = null;
                Tools.Log($"Distance Init failed!!!", Tools.ELogType.SystemLog);
                SysAlarm.AddErrorCodes(SysAlarm.DistanceConnErr);
            }
        }

        private void ReceiveTimerEvent(object sender, EventArgs e)
        {
            if (_port == null || !_port.IsOpen)
            {
                SysAlarm.AddErrorCodes(SysAlarm.DistanceConnErr);
                return;
            }

            try
            {
                // Read all available bytes from the TFmini sensor
                byte[] buffer = new byte[9];
                int bytesRead = _port.BytesToRead;
                byte[] tempBuffer = new byte[bytesRead];
                _port.Read(tempBuffer, 0, bytesRead);

                // Use the last 9 bytes from the read buffer
                if (bytesRead >= 9)
                {
                    Array.Copy(tempBuffer, bytesRead - 9, buffer, 0, 9);

                    // Check frame header (0x59 0x59)
                    if (buffer[0] == 0x59 && buffer[1] == 0x59)
                    {
                        // Distance is stored in bytes 3 and 4 (little endian)
                        int distance = buffer[2] + (buffer[3] << 8);

                        // Strength is stored in bytes 5 and 6 (little endian)
                        int strength = buffer[4] + (buffer[5] << 8);

                        // Quality check byte 7: if it's not 0, there's a signal issue
                        byte quality = buffer[6];

                        // Send the distance to the event aggregator
                        _eventAggregator.GetEvent<DistanceSensorEvent>().Publish(new DistanceSensorModel()
                        {
                            Distance_mm = distance * 10,
                            connected = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Distance Read Error: {ex.Message}", Tools.ELogType.SystemLog);
                SysAlarm.AddErrorCodes(SysAlarm.DistanceConnErr);
            }
        }
    }
}
