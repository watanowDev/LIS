using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using OpenCvSharp;
using Prism.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.VisionCam;
using WATA.LIS.VISION.CAM.MQTT;
using static WATA.LIS.Core.Common.Tools;
//using Emgu.CV;

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

        // Release publish와 Debug 환경 모두 지원하는 경로 계산
        private readonly static string modelDirectory = GetModelDirectory();
        private readonly static string detectorPrototxtPath = Path.Combine(modelDirectory, "detect.prototxt");
        private readonly static string detectorCaffeModelPath = Path.Combine(modelDirectory, "detect.caffemodel");
        private readonly static string superResolutionPrototxtPath = Path.Combine(modelDirectory, "sr.prototxt");
        private readonly static string superResolutionCaffeModelPath = Path.Combine(modelDirectory, "sr.caffemodel");

        private static string GetModelDirectory()
        {
            // Release publish: baseDirectory\Modules\WATA.LIS.VISION.CAM\Model
            string publishPath = Path.Combine(baseDirectory, "Modules", "WATA.LIS.VISION.CAM", "Model");
            if (Directory.Exists(publishPath))
                return publishPath;

            // Debug: 5단계 상위로 이동한 후 Modules 경로
            try
            {
                string debugPath = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(baseDirectory).FullName).FullName).FullName).FullName).FullName;
                return Path.Combine(debugPath, "Modules", "WATA.LIS.VISION.CAM", "Model");
            }
            catch
            {
                // 실패 시 기본 경로 반환
                return publishPath;
            }
        }


        // 소켓 관련
        private SubscriberSocket fork_leftFrameSocket;
        private SubscriberSocket fork_rightFrameSocket;
        private SubscriberSocket fork_depthFrameSocket;
        private SubscriberSocket fork_detectionDataSocket;
        private SubscriberSocket fork_videoFrameSocket;

        private Mat _mat = new Mat();

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        private Dictionary<string, double> depthValues = new Dictionary<string, double>();

        // 국가라벨 검출 관련
        //V2Detector _v2Detector;
        //List<V2DetectionModel> detectionDataList = new List<V2DetectionModel>();
        //private DispatcherTimer m_receiveObjectsTimer;

        private ConcurrentQueue<(byte[] FrameData, string Metadata)> rgbFrameQueue = new ConcurrentQueue<(byte[], string)>();
        private ConcurrentQueue<(byte[] FrameData, string Metadata)> depthFrameQueue = new ConcurrentQueue<(byte[], string)>();
        private ConcurrentQueue<(byte[] FrameData, string Metadata)> detectionDataQueue = new ConcurrentQueue<(byte[], string)>();

        // StreamingServer 블로킹 관련
        private readonly object _blockingLock = new object();
        private bool _isFrameReceived = false;
        private bool _isBlocked = true;
        private const int BLOCKING_TIMEOUT_MS = 40000; // 40초

        public Luxonis(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
        {
            _eventAggregator = eventAggregator;
            _visioncammodel = visioncammodel;
            visioncamConfig = (VisionCamConfigModel)_visioncammodel;

            // WeChatQRCode 초기화는 Init()에서 수행하도록 이동 (모델 파일 경로 문제로 인한 모듈 로딩 실패 방지)

            // 소켓 초기화 및 연결
            fork_leftFrameSocket = InitializeSocket("tcp://localhost:5561");
            fork_rightFrameSocket = InitializeSocket("tcp://localhost:5562");
            fork_depthFrameSocket = InitializeSocket("tcp://localhost:5563");
            fork_detectionDataSocket = InitializeSocket("tcp://localhost:5564");
            fork_videoFrameSocket = InitializeSocket("tcp://localhost:5565");
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
                Tools.Log("[VisionCam] Vision is disabled in config (vision_enable=0)", Tools.ELogType.SystemLog);
                return;
            }

            // WeChatQRCode 초기화 (파일 존재 여부 확인 후)
            try
            {
                Tools.Log($"[VisionCam] Model directory: {modelDirectory}", Tools.ELogType.SystemLog);

                // 모델 파일 존재 여부 확인
                if (!File.Exists(detectorPrototxtPath))
                {
                    Tools.Log($"[VisionCam] Model file not found: {detectorPrototxtPath}", Tools.ELogType.SystemLog);
                    Tools.Log("[VisionCam] QR detection will be disabled", Tools.ELogType.SystemLog);
                }
                else if (!File.Exists(detectorCaffeModelPath))
                {
                    Tools.Log($"[VisionCam] Model file not found: {detectorCaffeModelPath}", Tools.ELogType.SystemLog);
                    Tools.Log("[VisionCam] QR detection will be disabled", Tools.ELogType.SystemLog);
                }
                else if (!File.Exists(superResolutionPrototxtPath))
                {
                    Tools.Log($"[VisionCam] Model file not found: {superResolutionPrototxtPath}", Tools.ELogType.SystemLog);
                    Tools.Log("[VisionCam] QR detection will be disabled", Tools.ELogType.SystemLog);
                }
                else if (!File.Exists(superResolutionCaffeModelPath))
                {
                    Tools.Log($"[VisionCam] Model file not found: {superResolutionCaffeModelPath}", Tools.ELogType.SystemLog);
                    Tools.Log("[VisionCam] QR detection will be disabled", Tools.ELogType.SystemLog);
                }
                else
                {
                    Tools.Log("[VisionCam] Initializing WeChatQRCode with model files...", Tools.ELogType.SystemLog);
                    _weChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);
                    Tools.Log("[VisionCam] WeChatQRCode initialized successfully", Tools.ELogType.SystemLog);
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"[VisionCam] Failed to initialize WeChatQRCode: {ex.Message}", Tools.ELogType.SystemLog);
                Tools.Log("[VisionCam] QR detection will be disabled", Tools.ELogType.SystemLog);
            }

            //LoadModel(1); // GPU 인덱스 뭘로 해야하는지??
            InitializeMultiStream();

            // StreamingServer 블로킹 대기를 백그라운드 Task로 이동하여 UI 스레드 차단 방지
            Task.Run(() => WaitForFirstFrame());
        }

        private void WaitForFirstFrame()
        {
            Tools.Log("[StreamingServer] Waiting for camera frame from port 5565 (max 40 seconds)...", Tools.ELogType.SystemLog);
            Tools.Log("[StreamingServer] Vision processing will be blocked until frame is received or timeout.", Tools.ELogType.SystemLog);

            var stopwatch = Stopwatch.StartNew();

            while (!_isFrameReceived && stopwatch.ElapsedMilliseconds < BLOCKING_TIMEOUT_MS)
            {
                System.Threading.Thread.Sleep(100); // 100ms마다 체크
            }

            lock (_blockingLock)
            {
                _isBlocked = false;

                // 전역 블로킹 플래그 해제
                lock (GlobalValue.VisionBlockLock)
                {
                    GlobalValue.IsVisionStreamBlocked = false;
                }

                if (_isFrameReceived)
                {
                    Tools.Log($"[StreamingServer] Camera frame received after {stopwatch.ElapsedMilliseconds}ms. Vision processing unblocked.", Tools.ELogType.SystemLog);
                }
                else
                {
                    Tools.Log($"[StreamingServer] Timeout after {BLOCKING_TIMEOUT_MS}ms. Vision processing unblocked without frame.", Tools.ELogType.SystemLog);
                }
            }
        }

        public bool IsBlocked()
        {
            lock (_blockingLock)
            {
                return _isBlocked;
            }
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
                //Task.Factory.StartNew(() => ReceiveData(rear_detectionDataSocket, ProcessDetectionData_Rear), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                //Task.Factory.StartNew(() => ReceiveData(rear_videoFrameSocket, ProcessVideoFrame), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                // 프레임 처리 작업 시작
                ProcessVideoFrames();
                ProcessDepthFrames();
                //ProcessDetectionDataFrames();
                //m_receiveObjectsTimer.Start();
            }
            catch
            {
                //m_receiveObjectsTimer.Stop();
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
                    var frameData = message[0].Buffer;
                    var metadata = message[1].ConvertToString();

                    // 5565 포트(fork_videoFrameSocket)에서 첫 프레임 수신 시 블로킹 해제
                    if (subscriber == fork_videoFrameSocket && !_isFrameReceived)
                    {
                        lock (_blockingLock)
                        {
                            if (!_isFrameReceived)
                            {
                                _isFrameReceived = true;
                                Tools.Log("[StreamingServer] First camera frame received from port 5565.", Tools.ELogType.SystemLog);
                            }
                        }
                    }

                    // 큐에 프레임 추가 및 최대 프레임 수 제한
                    if (subscriber == fork_depthFrameSocket && depthFrameQueue.Count < 5)
                    {
                        depthFrameQueue.Enqueue((frameData, metadata));
                    }
                    else if (subscriber == fork_detectionDataSocket && detectionDataQueue.Count < 5)
                    {
                        detectionDataQueue.Enqueue((frameData, metadata));
                    }
                    else if (subscriber == fork_videoFrameSocket && rgbFrameQueue.Count < 5)
                    {
                        rgbFrameQueue.Enqueue((frameData, metadata));
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
            _mat = Mat.FromImageData(frameData, ImreadModes.Color);
            string qr = DetectQRCode(_mat);

            // Mat 객체를 byte 배열로 변환
            byte[] processedFrameData;
            using (var ms = new MemoryStream())
            {
                _mat.WriteToStream(ms, ".jpg");
                processedFrameData = ms.ToArray();
            }

            // 메타데이터에서 frameId 파싱
            string frameId = string.Empty;
            try
            {
                var jsonData = JsonConvert.DeserializeObject<Dictionary<string, string>>(metadata);
                if (jsonData != null && jsonData.ContainsKey("frameId"))
                {
                    frameId = jsonData["frameId"];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse frameId from metadata: {ex.Message}");
            }

            // 이벤트 데이터 객체 생성
            VisionCamModel eventModels = new VisionCamModel();
            eventModels.FRAME_ID = frameId; // 파싱한 frameId 할당
            eventModels.QR = qr;
            //eventModels.Objects = detectionDataList;
            eventModels.FRAME = frameData;
            eventModels.connected = true;
            if (depthValues.TryGetValue("BR", out double brDepth)) eventModels.BR_DEPTH = brDepth;
            if (depthValues.TryGetValue("BL", out double blDepth)) eventModels.BL_DEPTH = blDepth;
            if (depthValues.TryGetValue("MR", out double mrDepth)) eventModels.MR_DEPTH = mrDepth;
            if (depthValues.TryGetValue("ML", out double mlDepth)) eventModels.ML_DEPTH = mlDepth;
            if (depthValues.TryGetValue("TR", out double trDepth)) eventModels.TR_DEPTH = trDepth;
            if (depthValues.TryGetValue("TL", out double tlDepth)) eventModels.TL_DEPTH = tlDepth;

            _eventAggregator.GetEvent<VisionCamEvent>().Publish(eventModels);

            //SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
        }

        private void ProcessDepthFrames()
        {
            Task.Run(() =>
            {
                while (!tokenSource.Token.IsCancellationRequested)
                {
                    if (depthFrameQueue.TryDequeue(out var frame))
                    {
                        ProcessDepthFrame(frame.FrameData, frame.Metadata);
                    }
                }
            });
        }

        private void ProcessDetectionDataFrames()
        {
            Task.Run(() =>
            {
                while (!tokenSource.Token.IsCancellationRequested)
                {
                    if (detectionDataQueue.TryDequeue(out var frame))
                    {
                        ProcessDetectionData(frame.FrameData, frame.Metadata);
                    }
                }
            });
        }

        private void ProcessVideoFrames()
        {
            Task.Run(() =>
            {
                while (!tokenSource.Token.IsCancellationRequested)
                {
                    if (rgbFrameQueue.TryDequeue(out var frame))
                    {
                        ProcessVideoFrame(frame.FrameData, frame.Metadata);
                    }
                }
            });
        }

        // QR 코드 검출
        private string DetectQRCode(Mat frame)
        {
            string result = string.Empty;
            int maxY = int.MinValue;

            // WeChatQRCode가 초기화되지 않았으면 빈 문자열 반환
            if (_weChatQRCode == null)
            {
                return result;
            }

            try
            {
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
            }
            catch (Exception ex)
            {
                Tools.Log($"[VisionCam] QR detection error: {ex.Message}", Tools.ELogType.SystemLog);
            }

            return result;
        }

        // 국가코드 검출 쓰레드
        private void ReceiveObjectsTimerEvent(object sender, EventArgs e)
        {
            //// 테스트를 위해 이미지 파일을 로드
            //string testImagePath = @"C:\Users\USER\Source\Repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\NationTest.jpg";
            //if (File.Exists(testImagePath))
            //{
            //    _mat = Cv2.ImRead(testImagePath, ImreadModes.Color); // 이미지 파일을 Mat 객체로 로드
            //}
            //else
            //{
            //    Debug.WriteLine($"테스트 이미지 파일이 존재하지 않습니다: {testImagePath}");
            //    return;
            //}

            //// V2Detector를 사용하여 Inference 실행
            //detectionDataList = _v2Detector.Inference(_mat);
        }
    }
}