using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Model.VisionCam;

namespace WATA.LIS.VISION.CAM.MQTT
{
    public class DeepImgAnalysis : IDisposable
    {
        private readonly IEventAggregator _eventAggregator;

        // Pub: 원본 이미지+요청 JSON을 2차분석모델로 전송 (5003)
        private readonly PublisherSocket _pub;
        private readonly string _pubEndpoint;

        // Sub: 2차분석모델 결과 JSON 수신 (5004)
        private readonly SubscriberSocket _sub;
        private readonly string _subEndpoint;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _listenTask;

        public DeepImgAnalysis(IEventAggregator eventAggregator, string pubEndpoint = "tcp://localhost:5003", string subEndpoint = "tcp://localhost:5004")
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            _pubEndpoint = pubEndpoint;
            _pub = new PublisherSocket();
            _pub.Connect(_pubEndpoint);

            _subEndpoint = subEndpoint;
            _sub = new SubscriberSocket();
            _sub.Connect(_subEndpoint);
            _sub.Subscribe(""); // 모든 메시지 구독

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        }

        // 6자리 난수 생성
        public static string NewProductId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Span<char> buffer = stackalloc char[6];
            using var rng = RandomNumberGenerator.Create();
            byte[] data = new byte[6];
            rng.GetBytes(data);
            for (int i = 0; i < 6; i++) buffer[i] = chars[data[i] % chars.Length];
            return new string(buffer);
        }

        // 이미지와 JSON을 multipart로 발행 [image][json]
        public void Publish(DeepImgAnalysisPubModel model)
        {
            if (model == null || model.ImageBytes == null || model.ImageBytes.Length == 0)
                return;

            if (string.IsNullOrWhiteSpace(model.ProductID))
                model.ProductID = NewProductId();

            string json = JsonConvert.SerializeObject(model, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // multipart 메시지로 발행: [image][json]
            _pub.SendMoreFrame(model.ImageBytes).SendFrame(json);

            Tools.Log($"Published to {_pubEndpoint} | ProductID: {model.ProductID} | QR: {model.QR?.Count ?? 0} | Detections: {model.Detections?.Count ?? 0} | OCRs: {model.OcrList?.Count ?? 0}",
                Tools.ELogType.ActionLog);
        }

        // 5004 수신 루프
        private void ListenLoop(CancellationToken token)
        {
            Tools.Log($"DeepImgAnalysis result subscriber started on {_subEndpoint}", Tools.ELogType.ActionLog);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 멀티파트/싱글파트 모두 호환: 마지막 프레임을 JSON으로 처리
                    NetMQMessage msg = new NetMQMessage();
                    if (_sub.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(250), ref msg))
                    {
                        if (msg.FrameCount == 0) continue;

                        string json = msg[msg.FrameCount - 1].ConvertToString(Encoding.UTF8);
                        if (string.IsNullOrWhiteSpace(json)) continue;

                        var result = JsonConvert.DeserializeObject<DeepImgAnalysisSubModel>(json);
                        if (result != null)
                        {
                            Tools.Log($"Deep analysis received | ProductID: {result.ProductID} | QR: {result.QR?.Count ?? 0} | Detections: {result.Detections?.Count ?? 0} | OCRs: {result.OcrList?.Count ?? 0}",
                                Tools.ELogType.ActionLog);

                            _eventAggregator.GetEvent<DeepImgAnalysisSubEvent>().Publish(result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Tools.Log($"DeepImgAnalysis ListenLoop error: {ex.Message}", Tools.ELogType.SystemLog);
                    Thread.Sleep(100);
                }
            }

            Tools.Log("DeepImgAnalysis result subscriber stopped", Tools.ELogType.SystemLog);
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _listenTask?.Wait(500);
            }
            catch { }

            try { _pub?.Dispose(); } catch { }
            try { _sub?.Dispose(); } catch { }
            try { _cts?.Dispose(); } catch { }
        }
    }
}