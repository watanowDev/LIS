using NetMQ.Sockets;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using NetMQ;
using WATA.LIS.Core.Common;
using Newtonsoft.Json.Linq;
using System.Threading;
using WATA.LIS.Core.Model;
using WATA.LIS.Core.Model.LIVOX;
using WATA.LIS.Core.Events.LIVOX;

namespace WATA.LIS.SENSOR.LIVOX.MQTT
{
    public class PubSub
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILivoxModel _livoxmodel;

        LIVOXConfigModel livoxConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mSubTimer;
        private bool mConnected = false;
        private string mSubMsg = string.Empty;

        public PubSub(IEventAggregator eventAggregator, ILivoxModel livoxmodel)
        {
            _eventAggregator = eventAggregator;
            _livoxmodel = livoxmodel;
            livoxConfig = (LIVOXConfigModel)_livoxmodel;
        }

        public void Init()
        {
            mSubTimer = new DispatcherTimer();
            mSubTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            mSubTimer.Tick += new EventHandler(Subscribe);
            mSubTimer.Start();
        }

        private void Subscribe(object sender, EventArgs e)
        {
            try
            {
                //await Task.Run(() => {
                    
                //});

                using (var subscriber = new SubscriberSocket())
                {
                    // 서브스크라이버 소켓을 5555 포트에 연결합니다.
                    subscriber.Connect("tcp://127.0.0.1:5001");

                    // "VISION" 주제를 구독합니다.
                    subscriber.Subscribe("MID360>LIS");

                    // 타임아웃 설정 (예: 5초)
                    subscriber.Options.HeartbeatTimeout = TimeSpan.FromSeconds(5);

                    // 메시지를 수신합니다.
                    string RcvStr;
                    if (subscriber.TryReceiveFrameString(out RcvStr))
                    {
                        if (!RcvStr.Contains("MID360>LIS"))
                        {
                            return;
                        }

                        if (RcvStr.Contains("height") && RcvStr.Contains("width") && RcvStr.Contains("depth") && RcvStr.Contains("result"))
                        {
                            
                        }

                        // JSON 문자열에서 데이터를 추출합니다.
                        var jsonString = RcvStr.Substring(RcvStr.IndexOf("{"));
                        var jsonObject = JObject.Parse(jsonString);

                        LIVOXModel eventModel = new LIVOXModel();
                        eventModel.topic = "MID360>LIS";
                        eventModel.responseCode = 0;
                        eventModel.height = (int)jsonObject["height"];
                        eventModel.width = (int)jsonObject["width"];
                        eventModel.length = (int)jsonObject["length"];
                        eventModel.result = (int)jsonObject["result"]; // bool 값을 int로 변환

                        _eventAggregator.GetEvent<LIVOXEvent>().Publish(eventModel);
                        Tools.Log(RcvStr, Tools.ELogType.LIVOXLog);
                    }
                    else
                    {
                        // 타임아웃 발생 시 처리
                        Tools.Log("Timeout occurred while receiving message", Tools.ELogType.LIVOXLog);
                    }
                }
            }
            catch (Exception ex)
            {
                // 예외 처리
                Tools.Log($"Exception occurred: {ex.Message}", Tools.ELogType.LIVOXLog);
            }
        }
    }
}
