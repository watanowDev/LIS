using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
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

        public DetectionRcv(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            _subscriber = new SubscriberSocket();
            _subscriber.Connect("tcp://localhost:8070");
            _subscriber.Subscribe(""); // 모든 메시지 구독

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        }

        private void ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string message = _subscriber.ReceiveFrameString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        var model = JsonConvert.DeserializeObject<DectectionRcvModel>(message);
                        if (model != null)
                        {
                            _eventAggregator.GetEvent<DetectionRcvEvent>().Publish(model);
                        }
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