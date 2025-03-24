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
    public class SICK_LONG
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IDistanceModel _distancemodel;

        SerialPort m_serial = new SerialPort();
        SerialPort m_port = new SerialPort();

        DistanceConfigModel m_distanceConfig;

        private DispatcherTimer _receiveTimer;
        private HttpClient _httpClient;
        private readonly string _apiUrlStatus = "http://192.168.0.1/iolink/v1/openapi";
        private readonly string _apiUrlData = "http://192.168.0.1/iolink/v1/devices/master1port1/processdata/value";

        public SICK_LONG(IEventAggregator eventAggregator, IDistanceModel distancemodel)
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

                // 뒤의 2자리 값이 252, 0일 때만 처리
                if (valueArray.Length >= 6 && valueArray[4] == 252 && valueArray[5] == 0)
                {
                    // 앞 4자리 값을 16진수로 변환하여 붙인 후 10진수로 변환
                    string hexValue = string.Join("", valueArray.Take(4).Select(v => v.ToString("X2")));
                    int rawDistance = Convert.ToInt32(hexValue, 16);
                    int distance_mm = (rawDistance + (rawDistance % 10 >= 5 ? 5 : 0)) / 10;

                    // DistanceSensorModel 객체 생성 및 이벤트 발행
                    var distanceData = new DistanceSensorModel
                    {
                        Distance_mm = distance_mm,
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