using BlinkStickDotNet;
using ControlzEx.Standard;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.StatusLED;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.ErrorCheck;
using WATA.LIS.Core.Model.StatusLED;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.INDICATOR.LED.StatusLED
{
    public class Patlite_Docker_LAMP
    {
        private readonly IEventAggregator _eventAggregator;
        private Led_Buzzer_ConfigModel _ledBuzzer;
        private static readonly HttpClient _httpClient = new HttpClient();

        private List<string> responseBodies = new List<string>();
        private readonly int maxResponses = 20;

        DispatcherTimer dispatcherTimer;


        public Patlite_Docker_LAMP(IEventAggregator eventAggregator, ILedBuzzertModel ledBuzzer)
        {
            _eventAggregator = eventAggregator;
            _ledBuzzer = (Led_Buzzer_ConfigModel)ledBuzzer;

            _eventAggregator.GetEvent<Patlite_Docker_LAMP_Event>().Subscribe(OnLampEvent, ThreadOption.BackgroundThread, true);

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            dispatcherTimer.Tick += new EventHandler(MonitoringClearStatus);
            dispatcherTimer.Start();
        }

        public async Task Init()
        {
            try
            {
                string url = $"http://{_ledBuzzer.lamp_IP}/api/status?format=text";
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                Tools.Log($"Patlite Docker LAMP Init", Tools.ELogType.SystemLog);


            }
            catch (HttpRequestException e)
            {
                Tools.Log($"Patlite Docker LAMP Init failed", Tools.ELogType.SystemLog);
            }
            catch (Exception e)
            {
                Tools.Log($"Patlite Docker LAMP Init failed", Tools.ELogType.SystemLog);
            }
        }

        private void OnLampEvent(eLampSequence sequence)
        {
            string url = $"http://{_ledBuzzer.lamp_IP}/api/control?";

            switch (sequence)
            {
                case eLampSequence.Clear:
                    url += "clear=1";
                    break;
                case eLampSequence.Correct_Docking:
                    url += "alert=003300&color=11110";
                    break;
                case eLampSequence.Correct_Placement:
                    url += "alert=001100&color=11110";
                    break;
                case eLampSequence.Invalid_Docking:
                    url += "alert=660003&color=11110";
                    break;
                case eLampSequence.Invalid_Placement:
                    url += "alert=110000&color=11110";
                    break;
            }

            // Send request to Patlite Docker LAMP
            Task.Run(async () => {
                try
                {
                    HttpResponseMessage response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Tools.Log($"Docker LAMP Event: {sequence.ToString()}", Tools.ELogType.ActionLog);
                }
                catch (HttpRequestException e)
                {
                    Tools.Log($"Patlite Docker LAMP Event failed: {sequence.ToString()}", Tools.ELogType.SystemLog);
                }
                catch (Exception e)
                {
                    Tools.Log($"Patlite Docker LAMP Event failed: {sequence.ToString()}", Tools.ELogType.SystemLog);
                }
            });
        }

        private async void MonitoringClearStatus(object sender, EventArgs e)
        {
            try
            {
                string url = $"http://{_ledBuzzer.lamp_IP}/api/status?format=json";
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                // JSON 데이터를 파싱하여 Unit_Status 값을 확인
                var jsonResponse = JsonConvert.DeserializeObject<dynamic>(responseBody);
                var unitStatus = jsonResponse.Unit_Status.ToObject<List<int>>();

                // responseBody를 리스트에 추가
                responseBodies.Add(responseBody);
                if (responseBodies.Count > maxResponses)
                {
                    responseBodies.RemoveAt(0);
                }

                // Unit_Status가 [0, 0, 0, 0, 0]이 아닌 경우에만 동일한 상태가 20개 이상 쌓였는지 확인
                if (!unitStatus.SequenceEqual(new List<int> { 0, 0, 0, 0, 0 }) &&
                    responseBodies.Count == maxResponses && responseBodies.All(rb => rb == responseBodies[0]))
                {
                    _eventAggregator.GetEvent<Patlite_Docker_LAMP_Event>().Publish(eLampSequence.Clear);
                }
            }
            catch (HttpRequestException ex)
            {
                Tools.Log($"Patlite Docker LAMP Monitoring failed: {ex.Message}", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"Patlite Docker LAMP Monitoring failed: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }
    }
    /// <summary>
    /// 통신 프로토콜 및 관련 자료
    /// https://www.patlite.co.kr/product/detail0000000774.html
    /// </summary>
}