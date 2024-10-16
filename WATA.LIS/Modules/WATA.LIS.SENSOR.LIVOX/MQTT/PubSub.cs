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

        PublisherSocket _publisherSocket;
        SubscriberSocket _subscriberSocket;

        public PubSub(IEventAggregator eventAggregator, ILivoxModel livoxmodel)
        {
            _eventAggregator = eventAggregator;
            _livoxmodel = livoxmodel;
            livoxConfig = (LIVOXConfigModel)_livoxmodel;
        }

        public void Init()
        {
            InitLivox();
        }

        private void InitLivox()
        {
            try
            {
                _publisherSocket = new PublisherSocket();
                // 퍼블리셔 소켓을 5555 포트에 바인딩합니다.
                _publisherSocket.Bind("tcp://127.0.0.1:5002");

                Tools.Log($"InitLivox", Tools.ELogType.BackEndLog);

                _subscriberSocket = new SubscriberSocket();
                // 서브스크라이버 소켓을 5555 포트에 연결합니다.
                _subscriberSocket.Connect("tcp://127.0.0.1:5001");

                // 타임아웃 설정 (예: 30초)
                _subscriberSocket.Options.HeartbeatTimeout = TimeSpan.FromSeconds(30);
            }
            catch (Exception ex)
            {
                Tools.Log($"Failed InitLivox : {ex.Message}", Tools.ELogType.BackEndLog);
            }
        }

        private void SendToLivox(int commandNum)
        {
            try
            {
                // 메시지를 퍼블리시합니다.
                string message = $"LIS>MID360,{commandNum}"; // 1은 물류 부피 데이터 요청, 0은 수신완료 응답

                // 주제와 메시지를 결합하여 퍼블리시
                _publisherSocket.SendFrame(message);

                Tools.Log($"SendToLivox : {message}", Tools.ELogType.BackEndLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"Failed SendToLivox : {ex.Message}", Tools.ELogType.BackEndLog);
            }
        }

        private bool GetSizeData()
        {
            bool ret = false;
            try
            {
                // 이벤트 모델 생성
                LIVOXModel eventModel = new LIVOXModel();

                // "VISION" 주제를 구독합니다.
                _subscriberSocket.Subscribe("MID360>LIS");

                // 메시지를 수신합니다.
                string RcvStr = _subscriberSocket.ReceiveFrameString();
                if (!"".Equals(RcvStr))
                {
                    if (!RcvStr.Contains("MID360>LIS"))
                    {
                        return ret;
                    }

                    if (RcvStr.Contains("height") && RcvStr.Contains("width") && RcvStr.Contains("length") && RcvStr.Contains("result"))
                    {
                        // JSON 문자열에서 데이터를 추출합니다.
                        var jsonString = RcvStr.Substring(RcvStr.IndexOf("{"));
                        var jsonObject = JObject.Parse(jsonString);

                        eventModel.topic = "MID360>LIS";
                        eventModel.responseCode = 0;
                        eventModel.width = (int)jsonObject["width"];
                        eventModel.height = (int)jsonObject["height"];
                        eventModel.length = (int)jsonObject["length"];
                        eventModel.result = (int)jsonObject["result"]; // bool 값을 int로 변환
                        eventModel.points = jsonObject["points"].ToString();

                        _eventAggregator.GetEvent<LIVOXEvent>().Publish(eventModel);
                        Tools.Log($"height:{eventModel.width}, width:{eventModel.height}, depth:{eventModel.length}", Tools.ELogType.BackEndLog);

                        return ret = true;
                    }
                    else
                    {
                        // 부피사이즈를 읽어오지 못했을 때 처리
                        eventModel.width = -1;
                        eventModel.height = -1;
                        eventModel.length = -1;
                        eventModel.points = "";
                    }
                }
                else
                {
                    // 타임아웃 발생 시 처리
                    eventModel.width = -1;
                    eventModel.height = -1;
                    eventModel.length = -1;
                    eventModel.points = "";

                    Tools.Log("Timeout occurred while receiving message", Tools.ELogType.BackEndLog);
                }
            }
            catch (Exception ex)
            {
                // 예외 처리
                Tools.Log($"Exception occurred: {ex.Message}", Tools.ELogType.BackEndLog);
            }

            return ret;
        }
    }
}
