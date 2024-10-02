using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.QRCamera;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.VisionCam;
using WATA.LIS.Core.Model.SystemConfig;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ZXing;
using System.Drawing;
using System.Windows;

namespace WATA.LIS.VISION.CAM.Camera
{
    public class HIKVISION
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IQRCameraModel _qrcameramodel;


        QRCameraConfigModel qrcameraConfig;


        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mGetImageTimer;
        private bool mConnected = false;


        private uint iLastErr = 0;
        private Int32 m_lUserID = -1;
        private bool m_bInitSDK = false;


        public HIKVISION(IEventAggregator eventAggregator, IQRCameraModel qrcameramodel)
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

            //mCheckConnTimer = new DispatcherTimer();
            //mCheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            //mCheckConnTimer.Tick += new EventHandler(CheckConnTimer);
            //mCheckConnTimer.Start();

            mGetImageTimer = new DispatcherTimer();
            mGetImageTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            mGetImageTimer.Tick += new EventHandler(GetFrameTimer);
            mGetImageTimer.Start();

            VisionCamInit();
        }

        private void VisionCamInit()
        {
            VisionCamLogin();
        }

        /// <summary>
        /// VisionCam 연결
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void VisionCamLogin()
        {
            try
            {
                string DVRIPAddress = qrcameraConfig.vision_ip;
                Int16 DVRPortNumber = (Int16)qrcameraConfig.vision_port;
                string DVRUserName = qrcameraConfig.vision_id;
                string DVRPassword = qrcameraConfig.vision_pw;


                Tools.Log("VisionCam Login Success", Tools.ELogType.VisionCamLog);
                SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
            }
            catch (Exception ex)
            {
                Tools.Log($"VisionCamLogin Error : {ex.Message}", Tools.ELogType.VisionCamLog);
                SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
            }
        }



        /// <summary>
        /// 프레임 가져오기 및 QR코드 추출
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GetFrameTimer(object sender, EventArgs e)
        {
            GetFrame();
        }

        private void GetFrame()
        {
            // 카메라의 RTSP URL 설정
            string rtspUrl = $"rtsp://{qrcameraConfig.vision_id}:{qrcameraConfig.vision_pw}@{qrcameraConfig.vision_ip}:554/Streaming/Channels/101?transportmode=unicast";
            using var capture = new VideoCapture(rtspUrl);

            if (!capture.IsOpened())
            {
                SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                Tools.Log("VisionCam is not opened", Tools.ELogType.VisionCamLog);
                return;
            }

            // 프레임을 저장할 Mat 객체 생성
            using var frame = new Mat();

            capture.Read(frame);
            if (frame.Empty())
            {
                SysAlarm.AddErrorCodes(SysAlarm.VisionRcvErr);
                Tools.Log("VisionCam has no Image", Tools.ELogType.VisionCamLog);
                return;
            }

            // eventModels 생성
            VisionCamModel eventModels = new VisionCamModel();
            eventModels.QR = GetQRcodeID(frame);
            eventModels.STATUS = "NONE";
            eventModels.FRAME = frame.ToBytes();


            _eventAggregator.GetEvent<HikVisionEvent>().Publish(eventModels);
        }

        private string GetQRcodeID(Mat frame)
        {
            string ret = string.Empty;

            try
            {

                // OpenCvSharp Mat 객체를 Bitmap으로 변환
                using var bitmap = BitmapConverter.ToBitmap(frame);


                // ZXing 라이브러리를 사용하여 QR 코드 디코딩
                var reader = new BarcodeReader();
                var result = reader.Decode(bitmap);

                if (result != null)
                {
                    string qrCodeText = result.Text;
                    ret = qrCodeText;
                    Tools.Log($"QR Code Detected: {qrCodeText}", Tools.ELogType.VisionCamLog);
                }
                else
                {
                    //Tools.Log("No QR Code detected in the frame", Tools.ELogType.QRCamLog);
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Error while decoding QR Code: {ex.Message}", Tools.ELogType.VisionCamLog);
            }

            return ret;
        }
    }
}
