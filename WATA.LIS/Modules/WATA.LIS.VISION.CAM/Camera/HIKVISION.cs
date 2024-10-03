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
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Forms;
using System.Windows.Forms.Integration;


namespace WATA.LIS.VISION.CAM.Camera
{
    public class HIKVISION
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IQRCameraModel _qrcameramodel;

        VisionCamConfigModel qrcameraConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mGetImageTimer;
        private bool mConnected = false;
        private string mLastQRCode = string.Empty;

        private VideoCapture _capture;


        public HIKVISION(IEventAggregator eventAggregator, IQRCameraModel qrcameramodel)
        {
            _eventAggregator = eventAggregator;
            _qrcameramodel = qrcameramodel;
            qrcameraConfig = (VisionCamConfigModel)_qrcameramodel;
        }

        public void Init()
        {
            if (qrcameraConfig.vision_enable == 0)
            {
                return;
            }

            mCheckConnTimer = new DispatcherTimer();
            mCheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            mCheckConnTimer.Tick += new EventHandler(CheckConnection);

            mGetImageTimer = new DispatcherTimer();
            mGetImageTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            mGetImageTimer.Tick += new EventHandler(GetFrame);

            openVisionCam();
        }

        /// <summary>
        /// VisionCam 연결
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckConnection(object sender, EventArgs e)
        {
            if (!_capture.IsOpened())
            {
                try
                {
                    string rtspUrl = $"rtsp://{qrcameraConfig.vision_id}:{qrcameraConfig.vision_pw}@{qrcameraConfig.vision_ip}:554/Stream/Channels/101?transportmode=unicast";
                    _capture = new VideoCapture(rtspUrl);

                    if (!_capture.IsOpened())
                    {
                        SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                        Tools.Log("VisionCam is not opened", Tools.ELogType.VisionCamLog);
                        openVisionCam();
                        return;
                    }

                    Tools.Log("VisionCam Login again", Tools.ELogType.VisionCamLog);
                    SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
                }
                catch (Exception ex)
                {
                    Tools.Log($"VisionCamLogin Error : {ex.Message}", Tools.ELogType.VisionCamLog);
                    SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                }
            }
            else
            {
                Tools.Log("VisionCam is opened", Tools.ELogType.VisionCamLog);
                SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);
            }
        }

        private void openVisionCam()
        {
            try
            {
                // 카메라의 RTSP URL 설정
                string rtspUrl = $"rtsp://{qrcameraConfig.vision_id}:{qrcameraConfig.vision_pw}@{qrcameraConfig.vision_ip}:554/Stream/Channels/101?transportmode=unicast";
                _capture = new VideoCapture(rtspUrl);

                if (!_capture.IsOpened())
                {
                    SysAlarm.AddErrorCodes(SysAlarm.VisionConnErr);
                    Tools.Log("VisionCam is not opened", Tools.ELogType.VisionCamLog);
                    return;
                }


                Tools.Log("VisionCam Login Success", Tools.ELogType.VisionCamLog);
                SysAlarm.RemoveErrorCodes(SysAlarm.VisionConnErr);

                mCheckConnTimer.Start();
                mGetImageTimer.Start();
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
        private void GetFrame(object sender, EventArgs e)
        {
            using (var frame = new Mat())
            {
                _capture.Read(frame);

                if (!frame.Empty())
                {
                    byte[] currentFrameBytes = frame.ToBytes();

                    VisionCamModel eventModels = new VisionCamModel();
                    eventModels.QR = GetQRcodeID(frame);
                    eventModels.STATUS = "NONE";
                    eventModels.FRAME = currentFrameBytes;

                    if (eventModels.QR != mLastQRCode)
                    {
                        mLastQRCode = eventModels.QR;
                        WriteLog(); // QR 코드가 변경되었을 때만 로그 작성
                    }

                    _eventAggregator.GetEvent<HikVisionEvent>().Publish(eventModels);
                }
            }
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
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Error while decoding QR Code: {ex.Message}", Tools.ELogType.VisionCamLog);
            }

            return ret;
        }

        private void WriteLog()
        {
            if (!string.IsNullOrEmpty(mLastQRCode))
            {
                Tools.Log($"QR Code Detected: {mLastQRCode}", Tools.ELogType.VisionCamLog);
            }
        }
    }
}