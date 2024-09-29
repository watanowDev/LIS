using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using System.Net.Sockets;
using OpenCvSharp;
using Windows.Media;
using System.IO;
using System.Runtime.InteropServices;

namespace WATA.LIS.VISION.QRCamera.Camera
{
    public class HikVision
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IQRCameraModel _qrcameramodel;

        QRCameraConfigModel qrcameraConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mGetImageTimer;
        private bool mConnected = false;

        public HikVision(IEventAggregator eventAggregator, IQRCameraModel qrcameramodel)
        {
            _eventAggregator = eventAggregator;
            _qrcameramodel = qrcameramodel;
            qrcameraConfig = (QRCameraConfigModel)_qrcameramodel;
        }

        public void Init()
        {
            if (qrcameraConfig.vision_enable == 0)
            {
                return;
            }

            mCheckConnTimer = new DispatcherTimer();
            mCheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            mCheckConnTimer.Tick += new EventHandler(CheckConnTimer);
            mCheckConnTimer.Start();

            mGetImageTimer = new DispatcherTimer();
            mGetImageTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            mGetImageTimer.Tick += new EventHandler(GetImageTimer);
            mGetImageTimer.Start();

            QRCameraInit();
        }

        private void QRCameraInit()
        {
            CheckConnection();
        }

        private void CheckConnection()
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var result = client.BeginConnect(qrcameraConfig.vision_ip, 8000, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(5000, true);

                    if (!success)
                    {
                        return;
                    }

                    client.EndConnect(result);

                    mConnected = true;
                    SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
                    Tools.Log("VisionCam Connection Success", Tools.ELogType.QRCameraLog);

                    GetImage();
                }
            }
            catch
            {
                mConnected = false;
                SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                Tools.Log("VisionCam Connection Error", Tools.ELogType.QRCameraLog);
            }
        }

        private void GetImage()
        {
            try
            {
                // 카메라의 RTSP URL 설정
                string rtspUrl = $"rtsp://admin:wata2024@{qrcameraConfig.vision_ip}:554/Streaming/Channels/101?transportmode=unicast";

                // RTSP 스트림을 열기 위해 VideoCapture 객체 생성
                using var capture = new VideoCapture(rtspUrl);
                if (!capture.IsOpened())
                {
                    //Tools.Log("카메라 스트림을 열 수 없습니다.", Tools.ELogType.QRCameraLog);
                    return;
                }

                // 프레임을 저장할 Mat 객체 생성
                using var frame = new Mat();

                // 프레임 읽기
                capture.Read(frame);
                if (frame.Empty())
                {
                    //Tools.Log("프레임을 읽을 수 없습니다.", Tools.ELogType.QRCameraLog);
                    return;
                }

                // 프레임의 RGB 값을 확인
                CheckFrameRGBValues(frame);

                // 프레임을 파일로 저장 (선택 사항)
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captured_frame.jpg");
                Cv2.ImWrite(filePath, frame);
                //Tools.Log($"프레임이 저장되었습니다: {filePath}", Tools.ELogType.QRCameraLog);
            }
            catch (Exception ex)
            {
                //Tools.Log($"이미지를 가져오는 중 오류 발생: {ex.Message}", Tools.ELogType.QRCameraLog);
            }
        }

        private void CheckFrameRGBValues(Mat frame)
        {
            // 프레임의 크기와 채널 수 확인
            int rows = frame.Rows;
            int cols = frame.Cols;
            int channels = frame.Channels();

            //Tools.Log($"프레임 크기: {rows}x{cols}, 채널 수: {channels}", Tools.ELogType.QRCameraLog);

            // 프레임의 첫 번째 픽셀의 RGB 값 출력 (예시)
            Vec3b pixel = frame.At<Vec3b>(0, 0);
            //Tools.Log($"첫 번째 픽셀의 RGB 값: R={pixel.Item2}, G={pixel.Item1}, B={pixel.Item0}", Tools.ELogType.QRCameraLog);
        }

        private void GetImageTimer(object sender, EventArgs e)
        {

        }

        private void CheckConnTimer(object sender, EventArgs e)
        {
            
        }
    }
}
