using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.SENSOR.Distance.Sensor
{
    public class SICK_SHORT
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IDistanceModel _distancemodel;

        SerialPort m_serial = new SerialPort();
        SerialPort m_port = new SerialPort();

        DistanceConfigModel m_distanceConfig;

        private DispatcherTimer _receiveTimer;
        private HttpClient _httpClient;
        private readonly string _apiUrlStatus = "http://192.168.10.1/iolink/v1/openapi";
        private readonly string _apiUrlData = "http://192.168.10.1/iolink/v1/devices/master1port1/processdata/value";

        public SICK_SHORT(IEventAggregator eventAggregator, IDistanceModel distancemodel)
        {
            _eventAggregator = eventAggregator;
            _distancemodel = distancemodel;

            m_distanceConfig = (DistanceConfigModel)_distancemodel;

            _httpClient = new HttpClient();
        }

        public void SerialInit()
        {
            SerialThreadInit();

            _receiveTimer = new DispatcherTimer();
            _receiveTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            _receiveTimer.Tick += new EventHandler(ReceiveTimerEvent);
        }

        private async void SerialThreadInit()
        {
            try
            {
                var response = await _httpClient.GetAsync(_apiUrlStatus);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsStringAsync();

                if (data != null)
                {
                    _receiveTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"SerialThreadInit Error: {ex.Message}", Tools.ELogType.SystemLog);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private async void ReceiveTimerEvent(object sender, EventArgs e)
        {
            try
            {
                var response = await _httpClient.GetAsync(_apiUrlData);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsStringAsync();

                // JSON 데이터 파싱
                var jsonData = JsonConvert.DeserializeObject<dynamic>(data);
                byte[] valueArray = jsonData.getData.iolink.value.ToObject<byte[]>();

                // 앞 2자리만 사용
                if (valueArray.Length >= 2)
                {
                    // 2바이트를 16진수로 붙인 후 10진수로 변환
                    string hexValue = string.Join("", valueArray.Take(2).Select(v => v.ToString("X2")));
                    int rawDistance = Convert.ToInt32(hexValue, 16);

                    // DistanceSensorModel 객체 생성 및 이벤트 발행
                    var distanceData = new DistanceSensorModel
                    {
                        Distance_mm = rawDistance,
                        connected = true
                    };

                    _eventAggregator.GetEvent<DistanceSensorEvent>().Publish(distanceData);
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"ReceiveTimerEvent Error: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }
    }
}