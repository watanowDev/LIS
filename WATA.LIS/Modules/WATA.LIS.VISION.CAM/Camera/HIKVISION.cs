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


namespace WATA.LIS.VISION.CAM.Camera
{
    public class HIKVISION : System.Windows.Window
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IVisionCamModel _visioncammodel;

        VisionCamConfigModel visioncamConfig;

        private DispatcherTimer m_CheckConnTimer;
        private DispatcherTimer m_GetImageTimer;
        //private DispatcherTimer mCurrQRTimer;
        private bool m_Connected = false;
        public string m_LastQRCode = string.Empty;

        private int m_CameraIndex = 0;
        private VideoCapture m_Capture;
        Mat m_MatImage = new Mat();

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
            m_CheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            m_CheckConnTimer.Tick += new EventHandler(CheckConnection);

            m_GetImageTimer = new DispatcherTimer();
            m_GetImageTimer.Interval = new TimeSpan(0, 0, 0, 0, 33);
            m_GetImageTimer.Tick += new EventHandler(GetFrame);

            //mCurrQRTimer = new DispatcherTimer();
            //mCurrQRTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //mCurrQRTimer.Tick += new EventHandler(StampQRCode);

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
            string detectorPrototxtPath = @"C:\Users\wata_iot_dev\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.prototxt";
            string detectorCaffeModelPath = @"C:\Users\wata_iot_dev\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.caffemodel";
            string superResolutionPrototxtPath = @"C:\Users\wata_iot_dev\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.prototxt";
            string superResolutionCaffeModelPath = @"C:\Users\wata_iot_dev\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.caffemodel";

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
                Tools.Log("VisionCam Initiating...", Tools.ELogType.VisionCamLog);
                await Task.Run(() => {
                    // 카메라의 RTSP URL 설정
                    //string rtspUrl = $"rtsp://{visioncamConfig.vision_id}:{visioncamConfig.vision_pw}@{visioncamConfig.vision_ip}:554/Stream/Channels/101?transportmode=unicast";
                    //_capture = new VideoCapture(rtspUrl);

                    m_CameraIndex = 1;
                    m_Capture = new VideoCapture(m_CameraIndex);

                    if (m_Capture.IsOpened())
                    {
                        Tools.Log("VisionCam Open Success", Tools.ELogType.VisionCamLog);
                        SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);

                        //m_CheckConnTimer.Start();
                        m_GetImageTimer.Start();
                    }
                    else if (!m_Capture.IsOpened())
                    {
                        Tools.Log("VisionCam is not opened", Tools.ELogType.VisionCamLog);
                        SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                        return;
                    }
                });
            }
            catch (Exception ex)
            {
                Tools.Log($"VisionCam Open Error : {ex.Message}", Tools.ELogType.VisionCamLog);
                SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
            }
        }

        private void CheckConnection(object sender, EventArgs e)
        {
            if (!m_Capture.IsOpened())
            {
                SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                Tools.Log("VisionCam is disconnected", Tools.ELogType.VisionCamLog);

                try
                {
                    //string rtspUrl = $"rtsp://{visioncamConfig.vision_id}:{visioncamConfig.vision_pw}@{visioncamConfig.vision_ip}:554/Stream/Channels/101?transportmode=unicast";
                    //_capture = new VideoCapture(rtspUrl);
                    m_Capture = new VideoCapture(m_CameraIndex);

                    if (!m_Capture.IsOpened())
                    {
                        Tools.Log("VisionCam connection is failed", Tools.ELogType.VisionCamLog);
                        return;
                    }

                    Tools.Log("VisionCam Open again", Tools.ELogType.VisionCamLog);
                    SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
                }
                catch (Exception ex)
                {
                    Tools.Log($"VisionCam Open Error : {ex.Message}", Tools.ELogType.VisionCamLog);
                    SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                }
            }
            else
            {
                Tools.Log("VisionCam is alive", Tools.ELogType.VisionCamLog);
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
                m_Capture.Read(m_MatImage); // same as cvQueryFrame

                if (m_MatImage.Empty())
                {
                    Tools.Log($"No Image", Tools.ELogType.VisionCamLog);
                    return;
                }

                m_LastQRCode = GetQRcode(m_MatImage);
                byte[] currentFrameBytes = m_MatImage.ToBytes();

                VisionCamModel eventModels = new VisionCamModel();
                eventModels.QR = m_LastQRCode;
                eventModels.FRAME = currentFrameBytes;

                _eventAggregator.GetEvent<HikVisionEvent>().Publish(eventModels);
            }
            catch (Exception ex)
            {
                Tools.Log($"VisionCam Error : {ex.Message}", Tools.ELogType.VisionCamLog);
            }
        }

        private string GetQRcode(Mat frame)
        {
            string ret = string.Empty;

            try
            {
                // weChatQRCode 라이브러리를 사용하여 QR 코드 디코딩
                m_WeChatQRCode.DetectAndDecode(frame, out Mat[] bbox, out string[] results);

                if (results.Length > 0)
                {
                    ret = results[0];
                    //m_LastQRCode = ret;
                    Tools.Log($"WeChat QR : {m_LastQRCode}", Tools.ELogType.VisionCamLog);

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

                            // 바운딩 박스 그리기
                            Cv2.Polylines(frame, new[] { points }, isClosed: true, color: new Scalar(0, 255, 0), thickness: 2);
                        }
                    }
                }
                else
                {
                    //// ZXing 라이브러리를 사용하여 QR 코드 디코딩
                    //var reader = new BarcodeReader();
                    //var bitmap = BitmapConverter.ToBitmap(frame);
                    //// 비트맵에서 QR 코드 읽기
                    //var result = reader.Decode(bitmap);

                    //if (result != null)
                    //{
                    //    ret = result.Text;
                    //    Tools.Log($"ZXing QR : {m_LastQRCode}", Tools.ELogType.VisionCamLog);
                    //}
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Fail Read QR: {ex.Message}", Tools.ELogType.VisionCamLog);
            }

            return ret;
        }
    }
}