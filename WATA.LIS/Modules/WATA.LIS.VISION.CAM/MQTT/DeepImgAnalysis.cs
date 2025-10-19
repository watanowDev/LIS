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

        private const string PubTopic = "LIS>RefineModel"; // 요청 발행 토픽
        private const string SubTopic = "RefineModel>LIS"; // 결과 구독 토픽

        public DeepImgAnalysis(IEventAggregator eventAggregator, string pubEndpoint = "tcp://127.0.0.1:5003", string subEndpoint = "tcp://127.0.0.1:5004")
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            _pubEndpoint = pubEndpoint;
            _pub = new PublisherSocket();
            _pub.Bind(_pubEndpoint);
            Thread.Sleep(500);

            _subEndpoint = subEndpoint;
            _sub = new SubscriberSocket();
            _sub.Connect(_subEndpoint);
            _sub.Subscribe(SubTopic); // 지정 토픽만 구독

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));

            _eventAggregator.GetEvent<DeepImgAnalysisPubEvent>().Subscribe(Publish, ThreadOption.BackgroundThread, true);
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

        // ⭐ Single String Send 방식 (Python 테스트 코드와 동일)
        public void Publish(DeepImgAnalysisPubModel model)
        {
            if (model == null || model.ImageBytes == null || model.ImageBytes.Length == 0)
                return;

            if (string.IsNullOrWhiteSpace(model.ProductID))
                model.ProductID = "113";
                //model.ProductID = NewProductId();

            // ⭐ JSON 직렬화 (Image 필드에 Base64 자동 포함)
            string json = JsonConvert.SerializeObject(model, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // ⭐ Python 방식: "토픽 JSON" (공백으로 구분)
            string message = $"{PubTopic} {json}";
            _pub.SendFrame(message);

            Tools.Log($"Published to {_pubEndpoint} (topic='{PubTopic}') | ProductID: {model.ProductID} | Image: {model.ImageBytes.Length} bytes → Base64: {model.ImageBase64.Length} chars | QR: {model.QR?.Count ?? 0} | Detections: {model.Detections?.Count ?? 0}",
                Tools.ELogType.ActionLog);
        }

        // 5004 수신 루프 (기존과 동일 - Python이 멀티파트로 보낼 수도 있으므로 호환 유지)
        private void ListenLoop(CancellationToken token)
        {
            Tools.Log($"DeepImgAnalysis result subscriber started on {_subEndpoint} (topic='{SubTopic}')", Tools.ELogType.ActionLog);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    NetMQMessage msg = new NetMQMessage();
                    if (_sub.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(250), ref msg))
                    {
                        if (msg.FrameCount == 0) continue;

                        string json = string.Empty;

                        // ⭐ Single frame 처리 (Python이 "토픽 JSON" 형식으로 보낼 경우)
                        if (msg.FrameCount == 1)
                        {
                            string raw = msg[0].ConvertToString(Encoding.UTF8);
                            // "RefineModel>LIS {JSON}" 형식 파싱
                            var parts = raw.Split(new[] { ' ' }, 2);
                            if (parts.Length == 2 && parts[0] == SubTopic)
                            {
                                json = parts[1];
                            }
                        }
                        // ⭐ Multi frame 처리 (기존 방식 호환)
                        else if (msg.FrameCount >= 2)
                        {
                            string topic = msg[0].ConvertToString(Encoding.UTF8);
                            if (!string.Equals(topic, SubTopic, StringComparison.Ordinal))
                                continue;
                            json = msg[msg.FrameCount - 1].ConvertToString(Encoding.UTF8);
                        }

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