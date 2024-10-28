using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.VisionCam;
using WATA.LIS.Core.Model.SystemConfig;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ZXing;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Interop;
//using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Reflection;
using System.Threading;
using System.Data;
using OpenCvSharp.Internal.Vectors;
using OpenCvSharp.Internal;
using System.Windows.Media.Imaging;
using System.Diagnostics;


namespace WATA.LIS.VISION.CAM.Camera
{
    public class HIKVISION : System.Windows.Window
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IVisionCamModel _visioncammodel;

        VisionCamConfigModel visioncamConfig;

        private DispatcherTimer m_CheckConnTimer;
        private DispatcherTimer m_GetImageTimer;
        private DispatcherTimer m_GetQRSimpleTimer;
        //private DispatcherTimer mCurrQRTimer;
        private bool m_Connected = false;
        public string m_LastQRCode = string.Empty;

        private int m_CameraIndex = 0;
        private VideoCapture m_Capture;
        Mat m_MatImage = new Mat();
        private Stopwatch _stopwatch_fps = new Stopwatch();
        private Stopwatch _stopwatch_getqr = new Stopwatch();

        // weChatQRCode
        private WeChatQRCode m_WeChatQRCode;
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole(); // 콘솔 할당 함수


        public HIKVISION(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
        {
            _eventAggregator = eventAggregator;
            _visioncammodel = visioncammodel;
            visioncamConfig = (VisionCamConfigModel)_visioncammodel;
            AllocConsole();
        }

        public void Init()
        {
            if (visioncamConfig.vision_enable == 0)
            {
                return;
            }

            m_CheckConnTimer = new DispatcherTimer();
            m_CheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            m_CheckConnTimer.Tick += new EventHandler(CheckConnection);

            m_GetImageTimer = new DispatcherTimer();
            m_GetImageTimer.Interval = new TimeSpan(0, 0, 0, 0, 33);
            m_GetImageTimer.Tick += new EventHandler(GetFrame);

            InitializeWeChatQRCode();
            openVisionCam();
        }

        /// <summary>
        /// VisionCam 연결
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void InitializeWeChatQRCode()
        {
            string exePath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            //string detectorPrototxtPath = $"{exePath}\\sr.prototxt";
            //string detectorCaffeModelPath = $"{exePath}\\detect.caffemodel";
            //string superResolutionPrototxtPath = $"{exePath}\\sr.prototxt";
            //string superResolutionCaffeModelPath = $"{exePath}\\sr.caffemodel";
            string detectorPrototxtPath = @"C:\Users\USER\Source\Repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.prototxt";
            string detectorCaffeModelPath = @"C:\Users\USER\Source\Repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.caffemodel";
            string superResolutionPrototxtPath = @"C:\Users\USER\Source\Repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.prototxt";
            string superResolutionCaffeModelPath = @"C:\Users\USER\Source\Repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.caffemodel";

            // 모델 파일 경로 확인
            if (!File.Exists(detectorPrototxtPath) || !File.Exists(detectorCaffeModelPath) ||
                !File.Exists(superResolutionPrototxtPath) || !File.Exists(superResolutionCaffeModelPath))
            {
                throw new FileNotFoundException("One or more WeChatQRCode model files are missing.");
            }

            m_WeChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);
        }

        private async void openVisionCam()
        {
            try
            {
                Tools.Log("VisionCam Initiating...", Tools.ELogType.SystemLog);
                await Task.Run(() => {
                    // 카메라의 RTSP URL 설정
                    //string rtspUrl = $"rtsp://{visioncamConfig.vision_id}:{visioncamConfig.vision_pw}@{visioncamConfig.vision_ip}:554/Stream/Channels/101?transportmode=unicast";
                    //_capture = new VideoCapture(rtspUrl);
                    if (m_Capture != null) m_Capture.Dispose();

                    m_CameraIndex = 0;
                    m_Capture = new VideoCapture(m_CameraIndex);
                    m_Capture.FrameWidth = 3840 / 2;
                    m_Capture.FrameHeight = 2160 / 2;
                    m_Capture.Set(VideoCaptureProperties.Fps, 30); // 초당 프레임 설정
                    m_Capture.Set(VideoCaptureProperties.Focus, 1.5); // 카메라 초점거리 설정

                    if (m_Capture.IsOpened())
                    {
                        Tools.Log("VisionCam Open Success", Tools.ELogType.SystemLog);
                        SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);

                        //m_CheckConnTimer.Start();
                        m_GetImageTimer.Start();
                        //m_GetQRSimpleTimer.Start();
                    }
                    else if (!m_Capture.IsOpened())
                    {
                        Tools.Log("VisionCam is not opened", Tools.ELogType.SystemLog);
                        SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                        return;
                    }
                });
            }
            catch (Exception ex)
            {
                Tools.Log($"VisionCam Open Error : {ex.Message}", Tools.ELogType.SystemLog);
                SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
            }
        }

        private void CheckConnection(object sender, EventArgs e)
        {
            if (!m_Capture.IsOpened())
            {
                SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                Tools.Log("VisionCam is disconnected", Tools.ELogType.SystemLog);

                try
                {
                    //string rtspUrl = $"rtsp://{visioncamConfig.vision_id}:{visioncamConfig.vision_pw}@{visioncamConfig.vision_ip}:554/Stream/Channels/101?transportmode=unicast";
                    //_capture = new VideoCapture(rtspUrl);
                    m_Capture = new VideoCapture(m_CameraIndex);

                    if (!m_Capture.IsOpened())
                    {
                        Tools.Log("VisionCam connection is failed", Tools.ELogType.SystemLog);
                        return;
                    }

                    Tools.Log("VisionCam Open again", Tools.ELogType.SystemLog);
                    SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
                }
                catch (Exception ex)
                {
                    Tools.Log($"VisionCam Open Error : {ex.Message}", Tools.ELogType.SystemLog);
                    SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                }
            }
            else
            {
                Tools.Log("VisionCam is alive", Tools.ELogType.SystemLog);
                SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
            }
        }



        /// <summary>
        /// 프레임 가져오기 및 QR코드 추출
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GetFrame(object sender, EventArgs e)
        {
            try
            {
                _stopwatch_fps.Restart();
                m_Capture.Read(m_MatImage); // same as cvQueryFrame

                if (m_MatImage.Empty())
                {
                    //openVisionCam();
                    Tools.Log($"No Image", Tools.ELogType.SystemLog);
                    return;
                }

                //// Convert to grayscale
                //Mat grayImage = new Mat();
                //Cv2.CvtColor(m_MatImage, grayImage, ColorConversionCodes.BGR2GRAY);

                // FPS 계산
                _stopwatch_fps.Stop();
                double elapsedTime_fps = _stopwatch_fps.Elapsed.TotalSeconds;
                double _fps = 1.0 / elapsedTime_fps;

                _stopwatch_getqr.Restart();
                m_LastQRCode = GetQRcode(m_MatImage, 100);

                // Get QR Code 계산
                _stopwatch_getqr.Stop();
                double _getqr = _stopwatch_getqr.Elapsed.TotalMilliseconds;

                // Resize the frame
                Mat resizedFrame = new Mat();
                Cv2.Resize(m_MatImage, resizedFrame, new OpenCvSharp.Size(640, 480));
                Cv2.Rotate(m_MatImage, resizedFrame, RotateFlags.Rotate90Clockwise);

                // FPS 정보, GET QR 소요시간 프레임에 오버레이
                //Cv2.PutText(m_MatImage, $"FPS: {_fps:F2}", new OpenCvSharp.Point(150, 300), HersheyFonts.HersheySimplex, 10, Scalar.Red, 20);
                //Cv2.PutText(m_MatImage, $"GET QR: {_getqr:F2} ms", new OpenCvSharp.Point(150, 550), HersheyFonts.HersheySimplex, 10, Scalar.Red, 20);
                Cv2.PutText(resizedFrame, $"FPS: {_fps:F2}", new OpenCvSharp.Point(10, 100), HersheyFonts.HersheySimplex, 3, Scalar.Red, 7);
                Cv2.PutText(resizedFrame, $"GET QR: {_getqr:F2} ms", new OpenCvSharp.Point(10, 200), HersheyFonts.HersheySimplex, 3, Scalar.Red, 7);

                byte[] currentFrameBytes = resizedFrame.ToBytes();

                // Publish the event
                VisionCamModel eventModels = new VisionCamModel();
                eventModels.QR = m_LastQRCode;
                eventModels.FRAME = currentFrameBytes;
                eventModels.connected = true;

                _eventAggregator.GetEvent<HikVisionEvent>().Publish(eventModels);
            }
            catch (Exception ex)
            {
                Tools.Log($"VisionCam Error : {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        private string GetQRcode(Mat frame, int timeoutMilliseconds)
        {
            string ret = string.Empty;

            try
            {
                // frame 객체 유효성 검사
                if (frame == null || frame.Empty())
                {
                    Tools.Log("Invalid frame: frame is null or empty", Tools.ELogType.SystemLog);
                    return ret;
                }

                using (var cts = new CancellationTokenSource())
                {
                    cts.CancelAfter(timeoutMilliseconds);

                    var task = Task.Run(() => {
                        m_WeChatQRCode.DetectAndDecode(frame, out Mat[] bbox, out string[] results);

                        if (results.Length > 0)
                        {
                            ret = results[0];

                            // 바운딩 박스 그리기
                            foreach (var box in bbox)
                            {
                                // 바운딩 박스의 점들을 추출
                                if (box.Total() >= 4) // 최소 4개의 점이 필요
                                {
                                    OpenCvSharp.Point[] points = new OpenCvSharp.Point[4];
                                    for (int j = 0; j < 4; j++)
                                    {
                                        points[j] = new OpenCvSharp.Point((int)box.At<float>(j, 0), (int)box.At<float>(j, 1));
                                    }

                                    // 바운딩 박스 그리기 (빨간색, 굵기 4)
                                    Cv2.Polylines(frame, new[] { points }, isClosed: true, color: new Scalar(0, 0, 255), thickness: 15);
                                }
                            }
                        }
                    }, cts.Token);

                    try
                    {
                        task.Wait(cts.Token); // 동기적으로 작업을 기다림
                    }
                    catch (OperationCanceledException)
                    {
                        //Tools.Log("GetQRcode operation timed out", Tools.ELogType.SystemLog);
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Fail Read QR: {ex.Message}", Tools.ELogType.SystemLog);
            }

            return ret;
        }
    }
}