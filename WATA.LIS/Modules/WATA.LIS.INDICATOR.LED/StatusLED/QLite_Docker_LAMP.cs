using BlinkStickDotNet;
using ControlzEx.Standard;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Threading;
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
    public class QLite_Docker_LAMP
    {
        private readonly IEventAggregator _eventAggregator;
        private Led_Buzzer_ConfigModel _ledBuzzer;
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly DispatcherTimer _clearTimer;
        private readonly TimeSpan _clearTimeout = TimeSpan.FromSeconds(10);

        public QLite_Docker_LAMP(IEventAggregator eventAggregator, ILedBuzzertModel ledBuzzer)
        {
            _eventAggregator = eventAggregator;
            _ledBuzzer = (Led_Buzzer_ConfigModel)ledBuzzer;

            _eventAggregator.GetEvent<Patlite_Docker_LAMP_Event>().Subscribe(OnLampEvent, ThreadOption.BackgroundThread, true);

            _clearTimer = new DispatcherTimer();
            _clearTimer.Interval = _clearTimeout;
            _clearTimer.Tick += new EventHandler(ClearTimerEvent);

            // 최초 시작 시 무조건 한 번 Clear 수행 및 타이머 시작
            OnLampEvent(eLampSequence.Clear);
        }

        private void ClearTimerEvent(object sender, EventArgs e)
        {
            // 타이머가 만료되면 Clear 이벤트를 발생시키고, 타이머를 다시 시작
            OnLampEvent(eLampSequence.Clear);
        }

        private void ResetClearTimer()
        {
            _clearTimer.Stop();
            _clearTimer.Start();
        }

        private void OnLampEvent(eLampSequence sequence)
        {
            // 어떤 이벤트든 타이머를 리셋 (Clear 포함)
            ResetClearTimer();

            string baseUrl = $"http://{_ledBuzzer.lamp_IP}/L?";

            try
            {
                if (sequence == eLampSequence.Clear)
                {
                    var response1 = _httpClient.GetAsync(baseUrl + "1=3");
                    Thread.Sleep(50);
                    var response2 = _httpClient.GetAsync(baseUrl + "3=3");
                    Thread.Sleep(50);
                    var response3 = _httpClient.GetAsync(baseUrl + "S=0");
                    Thread.Sleep(50);
                }
                else if (sequence == eLampSequence.Correct_Docking)
                {
                    var response1 = _httpClient.GetAsync(baseUrl + "1=3");
                    Thread.Sleep(50);
                    var response2 = _httpClient.GetAsync(baseUrl + "3=2");
                    Thread.Sleep(50);
                    var response3 = _httpClient.GetAsync(baseUrl + "S=0");
                    Thread.Sleep(50);
                }
                else if (sequence == eLampSequence.Correct_Placement)
                {
                    var response1 = _httpClient.GetAsync(baseUrl + "1=3");
                    Thread.Sleep(50);
                    var response2 = _httpClient.GetAsync(baseUrl + "3=1");
                    Thread.Sleep(50);
                    var response3 = _httpClient.GetAsync(baseUrl + "S=0");
                    Thread.Sleep(50);
                }
                else if (sequence == eLampSequence.Invalid_Docking)
                {
                    var response1 = _httpClient.GetAsync(baseUrl + "1=2");
                    Thread.Sleep(50);
                    var response2 = _httpClient.GetAsync(baseUrl + "3=3");
                    Thread.Sleep(50);
                    var response3 = _httpClient.GetAsync(baseUrl + "S=4");
                    Thread.Sleep(50);
                }
                else if (sequence == eLampSequence.Invalid_Placement)
                {
                    var response1 = _httpClient.GetAsync(baseUrl + "1=1");
                    Thread.Sleep(50);
                    var response2 = _httpClient.GetAsync(baseUrl + "3=3");
                    Thread.Sleep(50);
                    var response3 = _httpClient.GetAsync(baseUrl + "S=0");
                    Thread.Sleep(50);
                }

                if (sequence != eLampSequence.Clear)
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
        }
    }

    /// <summary>
    /// 통신 프로토콜 및 관련 자료
    /// http://_ledBuzzer.lamp_IP/L?1=1    빨강 점등
    /// http://_ledBuzzer.lamp_IP/L?1=2    빨강 점멸
    /// http://_ledBuzzer.lamp_IP/L?1=3    빨강 끄기
    /// http://_ledBuzzer.lamp_IP/L?3=1    초록 점등
    /// http://_ledBuzzer.lamp_IP/L?3=2    초록 점멸
    /// http://_ledBuzzer.lamp_IP/L?3=3    초록 끄기
    /// http://_ledBuzzer.lamp_IP/L?S=0    알림 끄기
    /// http://_ledBuzzer.lamp_IP/L?S=4    알림 켜기
    /// </summary>
}