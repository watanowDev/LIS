using OpenCvSharp;
using Prism.Events;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Model.VisionCam;
using System.Diagnostics;
using System.Collections.Generic;

namespace WATA.LIS.VISION.CAM.Camera
{
    public class Luxonis
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IVisionCamModel _visioncammodel;

        VisionCamConfigModel visioncamConfig;

        private WeChatQRCode _weChatQRCode;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        // WeChatQRCode 모델 경로
        private readonly static string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly static string projectRootDirectory = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(baseDirectory).FullName).FullName).FullName).FullName).FullName;
        private readonly static string modelDirectory = Path.Combine(projectRootDirectory, "Modules", "WATA.LIS.VISION.CAM", "Model");
        private readonly static string detectorPrototxtPath = Path.Combine(modelDirectory, "detect.prototxt");
        private readonly static string detectorCaffeModelPath = Path.Combine(modelDirectory, "detect.caffemodel");
        private readonly static string superResolutionPrototxtPath = Path.Combine(modelDirectory, "sr.prototxt");
        private readonly static string superResolutionCaffeModelPath = Path.Combine(modelDirectory, "sr.caffemodel");

        private SubscriberSocket _leftFrameSocket;
        private SubscriberSocket _rightFrameSocket;
        private SubscriberSocket _depthFrameSocket;
        private SubscriberSocket _detectionDataSocket;
        private SubscriberSocket _videoFrameSocket;

        private Dictionary<string, double> depthValues = new Dictionary<string, double>();

        // Define ROIs
        private readonly Dictionary<string, (int X, int Y, int Width, int Height)> rois = new Dictionary<string, (int, int, int, int)>
        {
            { "TM", (640, 185, 25, 25) },  // Top Middle
            { "MR", (840, 385, 25, 25) },  // Middle Right
            { "MM", (640, 385, 25, 25) },  // Middle Middle
            { "ML", (440, 385, 25, 25) },  // Middle Left
            { "BR", (740, 585, 25, 25) },  // Bottom Right
            { "BM", (640, 585, 25, 25) },  // Bottom Middle
            { "BL", (540, 585, 25, 25) }   // Bottom Left
        };

        public Luxonis(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
        {
            _eventAggregator = eventAggregator;
            _visioncammodel = visioncammodel;
            visioncamConfig = (VisionCamConfigModel)_visioncammodel;

            // WeChatQRCode 초기화
            _weChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);

            // 소켓 초기화 및 연결
            _leftFrameSocket = InitializeSocket("tcp://localhost:5561");
            _rightFrameSocket = InitializeSocket("tcp://localhost:5562");
            _depthFrameSocket = InitializeSocket("tcp://localhost:5563");
            _detectionDataSocket = InitializeSocket("tcp://localhost:5564");
            _videoFrameSocket = InitializeSocket("tcp://localhost:5565");
        }

        private SubscriberSocket InitializeSocket(string address)
        {
            var socket = new SubscriberSocket();
            socket.Connect(address);
            socket.SubscribeToAnyTopic();
            return socket;
        }

        public void Init()
        {
            if (visioncamConfig.vision_enable == 0)
            {
                return;
            }

            InitializeMultiStream();
        }

        private void InitializeMultiStream()
        {
            try
            {
                //Task.Factory.StartNew(() => ReceiveData(_leftFrameSocket, ProcessLeftFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                //Task.Factory.StartNew(() => ReceiveData(_rightFrameSocket, ProcessRightFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(() => ReceiveData(_depthFrameSocket, ProcessDepthFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(() => ReceiveData(_detectionDataSocket, ProcessDetectionData), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(() => ReceiveData(_videoFrameSocket, ProcessVideoFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void ReceiveData(SubscriberSocket subscriber, Action<byte[], string> processData)
        {
            try
            {
                while (!tokenSource.Token.IsCancellationRequested)
                {
                    var message = subscriber.ReceiveMultipartMessage();
                    if (subscriber == _detectionDataSocket)
                    {
                        var metadata = message[0].ConvertToString();
                        processData(null, metadata);
                    }
                    else
                    {
                        var frameData = message[0].Buffer;
                        var metadata = message[1].ConvertToString();
                        processData(frameData, metadata);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{subscriber.Options.LastEndpoint} ReceiveData Error: {ex.Message}");
            }
        }

        private void ProcessLeftFrame(byte[] frameData, string metadata)
        {
            var frame = Mat.FromImageData(frameData, ImreadModes.Grayscale);
            // 처리 로직 추가
        }

        private void ProcessRightFrame(byte[] frameData, string metadata)
        {
            var frame = Mat.FromImageData(frameData, ImreadModes.Grayscale);
            // 처리 로직 추가
        }

        private void ProcessDepthFrame(byte[] frameData, string metadata)
        {
            // metadata에서 depth 값을 파싱하여 depthValues에 저장
            var jsonData = JsonConvert.DeserializeObject<Dictionary<string, object>>(metadata);
            if (jsonData != null && jsonData.ContainsKey("roi_depths"))
            {
                var roiDepths = JsonConvert.DeserializeObject<Dictionary<string, double>>(jsonData["roi_depths"].ToString());
                foreach (var kvp in roiDepths)
                {
                    depthValues[kvp.Key] = kvp.Value;
                }
            }
        }

        private void ProcessDetectionData(byte[] frameData, string metadata)
        {
            var detectionData = JsonConvert.DeserializeObject<VisionDetectionModel>(metadata);

            // 이벤트 데이터 객체 생성
            _eventAggregator.GetEvent<VisionDetectionEvent>().Publish(detectionData);
        }

        private void ProcessVideoFrame(byte[] frameData, string metadata)
        {
            // QR 코드 검출 및 사각형 그리기
            Mat mat = Mat.FromImageData(frameData, ImreadModes.Color);
            string qr = DetectQRCode(mat);

            // Mat 객체를 byte 배열로 변환
            byte[] processedFrameData;
            using (var ms = new MemoryStream())
            {
                mat.WriteToStream(ms, ".jpg");
                processedFrameData = ms.ToArray();
            }

            // 이벤트 데이터 객체 생성
            VisionCamModel eventModels = new VisionCamModel();
            eventModels.QR = qr;
            eventModels.FRAME = processedFrameData;
            eventModels.connected = true;
            if (depthValues.TryGetValue("TM", out double tmDepth)) eventModels.TM_DEPTH = tmDepth;
            if (depthValues.TryGetValue("MR", out double mrDepth)) eventModels.MR_DEPTH = mrDepth;
            if (depthValues.TryGetValue("MM", out double mmDepth)) eventModels.MM_DEPTH = mmDepth;
            if (depthValues.TryGetValue("ML", out double mlDepth)) eventModels.ML_DEPTH = mlDepth;
            if (depthValues.TryGetValue("BR", out double brDepth)) eventModels.BR_DEPTH = brDepth;
            if (depthValues.TryGetValue("BM", out double bmDepth)) eventModels.BM_DEPTH = bmDepth;
            if (depthValues.TryGetValue("BL", out double blDepth)) eventModels.BL_DEPTH = blDepth;

            _eventAggregator.GetEvent<VisionCamEvent>().Publish(eventModels);
        }

        private string DetectQRCode(Mat frame)
        {
            string result = string.Empty;
            int maxY = int.MinValue;

            // WeChat QR 코드 인식
            _weChatQRCode.DetectAndDecode(frame, out Mat[] bbox, out string[] weChatResult);

            if (weChatResult != null && weChatResult.Length > 0)
            {
                for (int i = 0; i < weChatResult.Length; i++)
                {
                    var qrCode = weChatResult[i];
                    var box = bbox[i];

                    // QR 코드 위치에 사각형 그리기
                    if (box.Total() >= 4)
                    {
                        var detectedQRpoints = new Point[4];
                        for (int j = 0; j < 4; j++)
                        {
                            detectedQRpoints[j] = new Point((int)box.At<float>(j, 0), (int)box.At<float>(j, 1));
                        }

                        int minX = detectedQRpoints.Min(p => p.X);
                        int minY = detectedQRpoints.Min(p => p.Y);
                        int maxX = detectedQRpoints.Max(p => p.X);
                        int currentMaxY = detectedQRpoints.Max(p => p.Y);

                        var qrRect = new Rect(minX, minY, maxX - minX, currentMaxY - minY);
                        Cv2.Rectangle(frame, qrRect, new Scalar(0, 255, 0), 3);

                        // 가장 아래쪽에 있는 QR 코드를 선택
                        if (currentMaxY > maxY)
                        {
                            maxY = currentMaxY;
                            result = qrCode;
                        }
                    }
                }
            }
            return result;
        }
    }
}