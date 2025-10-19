using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Model.VisionCam;

namespace WATA.LIS.VISION.CAM.MQTT
{
    internal class DetectionRcv : IDisposable
    {
        private readonly SubscriberSocket _subscriber;
        private readonly CancellationTokenSource _cts;
        private readonly Task _listenTask;
        private readonly IEventAggregator _eventAggregator;

        private const string ExcludedTopic = "RefineModel>LIS"; // 제외할 토픽

        public DetectionRcv(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            _subscriber = new SubscriberSocket();
            _subscriber.Connect("tcp://localhost:8070");
            _subscriber.Subscribe(""); // 모든 메시지 구독 후 코드에서 제외 토픽 필터링

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        }

        private void ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    NetMQMessage msg = new NetMQMessage();
                    if (!_subscriber.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(250), ref msg))
                        continue;

                    if (msg.FrameCount == 0)
                        continue;

                    // 멀티파트일 경우 첫 프레임은 토픽으로 간주하고 제외 토픽이면 무시
                    string payload;
                    if (msg.FrameCount >= 2)
                    {
                        string topic = msg[0].ConvertToString(Encoding.UTF8);
                        if (string.Equals(topic, ExcludedTopic, StringComparison.Ordinal))
                            continue;

                        payload = msg[msg.FrameCount - 1].ConvertToString(Encoding.UTF8);
                    }
                    else
                    {
                        // 싱글파트 메시지(토픽 없음)는 그대로 payload로 처리
                        payload = msg[0].ConvertToString(Encoding.UTF8);
                    }

                    if (string.IsNullOrWhiteSpace(payload))
                        continue;

                    var model = JsonConvert.DeserializeObject<DectectionRcvModel>(payload);
                    if (model != null)
                    {
                        _eventAggregator.GetEvent<DetectionRcvEvent>().Publish(model);
                    }
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _listenTask?.Wait(500);
            _subscriber?.Dispose();
            _cts?.Dispose();
        }
    }
}