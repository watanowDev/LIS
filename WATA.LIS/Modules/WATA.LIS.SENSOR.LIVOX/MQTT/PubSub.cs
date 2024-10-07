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

namespace WATA.LIS.SENSOR.LIVOX.MQTT
{
    public class PubSub
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILivoxModel _livoxmodel;

        LIVOXConfigModel livoxConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mPubTimer;
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
            mSubTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            mSubTimer.Tick += new EventHandler(Subscribe);
            mSubTimer.Start();

            mPubTimer = new DispatcherTimer();
            mPubTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            mPubTimer.Tick += new EventHandler(Publish);
            mPubTimer.Start();
        }

        private void Subscribe(object sender, EventArgs e)
        {
            try
            {
                using (var subscriber = new SubscriberSocket())
                {
                    // 서브스크라이버 소켓을 5555 포트에 연결합니다.
                    subscriber.Connect("tcp://192.168.219.186:5001");

                    // "VISION" 주제를 구독합니다.
                    subscriber.Subscribe("MID360>LIS");

                    // 타임아웃 설정 (예: 5초)
                    subscriber.Options.HeartbeatTimeout = TimeSpan.FromSeconds(5);

                    // 메시지를 수신합니다.
                    string RcvStr;
                    if (subscriber.TryReceiveFrameString(out RcvStr))
                    {
                        if (!RcvStr.Contains("LIS>MID360"))
                        {
                            return;
                        }

                        if (RcvStr.Contains("height") && RcvStr.Contains("width") && RcvStr.Contains("depth") && RcvStr.Contains("true"))
                        {
                            mPubTimer.Stop();
                            Tools.Log(RcvStr, Tools.ELogType.LIVOXLog);
                        }
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

        private void Publish(object sender, EventArgs e)
        {
            try
            {
                using (var publisher = new PublisherSocket())
                {
                    // 퍼블리셔 소켓을 5555 포트에 바인딩합니다.
                    publisher.Bind("tcp://192.168.219.193:5002");

                    // 메시지를 퍼블리시합니다.
                    LIVOXModel eventModel = new LIVOXModel();
                    string topic = eventModel.topic;
                    string message = $"{eventModel.responseCode}";

                    // 주제와 메시지를 결합하여 퍼블리시
                    publisher.SendMoreFrame(topic).SendFrame(message);
                }
            }
            catch
            {

            }
        }
    }
}
