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
using WATA.LIS.Core.Common;
using static WATA.LIS.Core.Common.Tools;
using System.Security.Policy;

namespace WATA.LIS.VISION.CAM.Camera
{
    public class Luxonis
    {
        // Config 관련
        private readonly IEventAggregator _eventAggregator;
        private readonly IVisionCamModel _visioncammodel;
        VisionCamConfigModel visioncamConfig;

        // WeChatQRCode 모델 경로
        private WeChatQRCode _weChatQRCode;
        private readonly static string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly static string projectRootDirectory = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(baseDirectory).FullName).FullName).FullName).FullName).FullName;
        private readonly static string modelDirectory = Path.Combine(projectRootDirectory, "Modules", "WATA.LIS.VISION.CAM", "Model");
        private readonly static string detectorPrototxtPath = Path.Combine(modelDirectory, "detect.prototxt");
        private readonly static string detectorCaffeModelPath = Path.Combine(modelDirectory, "detect.caffemodel");
        private readonly static string superResolutionPrototxtPath = Path.Combine(modelDirectory, "sr.prototxt");
        private readonly static string superResolutionCaffeModelPath = Path.Combine(modelDirectory, "sr.caffemodel");


        // 소켓 관련
        private SubscriberSocket fork_leftFrameSocket;
        private SubscriberSocket fork_rightFrameSocket;
        private SubscriberSocket fork_depthFrameSocket;
        private SubscriberSocket fork_detectionDataSocket;
        private SubscriberSocket fork_videoFrameSocket;

        private SubscriberSocket front_leftFrameSocket;
        private SubscriberSocket front_rightFrameSocket;
        private SubscriberSocket front_depthFrameSocket;
        private SubscriberSocket front_detectionDataSocket;
        private SubscriberSocket front_videoFrameSocket;

        private SubscriberSocket rear_leftFrameSocket;
        private SubscriberSocket rear_rightFrameSocket;
        private SubscriberSocket rear_depthFrameSocket;
        private SubscriberSocket rear_detectionDataSocket;
        private SubscriberSocket rear_videoFrameSocket;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        private Dictionary<string, double> depthValues = new Dictionary<string, double>();


        public Luxonis(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
        {
            _eventAggregator = eventAggregator;
            _visioncammodel = visioncammodel;
            visioncamConfig = (VisionCamConfigModel)_visioncammodel;

            // WeChatQRCode 초기화
            _weChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);

            // 소켓 초기화 및 연결
            fork_leftFrameSocket = InitializeSocket("tcp://localhost:5561");
            fork_rightFrameSocket = InitializeSocket("tcp://localhost:5562");
            fork_depthFrameSocket = InitializeSocket("tcp://localhost:5563");
            fork_detectionDataSocket = InitializeSocket("tcp://localhost:5564");
            fork_videoFrameSocket = InitializeSocket("tcp://localhost:5565");

            //front_leftFrameSocket = InitializeSocket("tcp://localhost:5571");
            //front_rightFrameSocket = InitializeSocket("tcp://localhost:5572");
            //front_depthFrameSocket = InitializeSocket("tcp://localhost:5573");
            //front_detectionDataSocket = InitializeSocket("tcp://localhost:5574");
            //front_videoFrameSocket = InitializeSocket("tcp://localhost:5575");

            //rear_leftFrameSocket = InitializeSocket("tcp://localhost:5581");
            //rear_rightFrameSocket = InitializeSocket("tcp://localhost:5582");
            //rear_depthFrameSocket = InitializeSocket("tcp://localhost:5583");
            rear_detectionDataSocket = InitializeSocket("tcp://localhost:5584");
            //rear_videoFrameSocket = InitializeSocket("tcp://localhost:5585");
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

            //LoadModel(1); // GPU 인덱스 뭘로 해야하는지??
            InitializeMultiStream();
        }

        private void InitializeMultiStream()
        {
            try
            {
                //Task.Factory.StartNew(() => ReceiveData(_leftFrameSocket, ProcessLeftFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                //Task.Factory.StartNew(() => ReceiveData(_rightFrameSocket, ProcessRightFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(() => ReceiveData(fork_depthFrameSocket, ProcessDepthFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(() => ReceiveData(fork_detectionDataSocket, ProcessDetectionData), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(() => ReceiveData(fork_videoFrameSocket, ProcessVideoFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                //Task.Factory.StartNew(() => ReceiveData(front_depthFrameSocket, ProcessDepthFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                //Task.Factory.StartNew(() => ReceiveData(front_detectionDataSocket, ProcessDetectionData), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                //Task.Factory.StartNew(() => ReceiveData(front_videoFrameSocket, ProcessVideoFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                //Task.Factory.StartNew(() => ReceiveData(rear_depthFrameSocket, ProcessDepthFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(() => ReceiveData(rear_detectionDataSocket, ProcessDetectionData), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                //Task.Factory.StartNew(() => ReceiveData(rear_videoFrameSocket, ProcessVideoFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
                    if (subscriber == fork_detectionDataSocket)
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

        // 이벤트 데이터 객체 생성
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
            if (depthValues.TryGetValue("BR", out double brDepth)) eventModels.BR_DEPTH = brDepth;
            if (depthValues.TryGetValue("BL", out double blDepth)) eventModels.BL_DEPTH = blDepth;

            _eventAggregator.GetEvent<VisionCamEvent>().Publish(eventModels);
        }

        // QR 코드 검출
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