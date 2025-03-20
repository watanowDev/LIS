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
using System.Windows.Forms.Integration;
using System.Reflection;
using System.Threading;
using System.Data;
using OpenCvSharp.Internal.Vectors;
using OpenCvSharp.Internal;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Windows.Media;
using Orbbec;
using System.Diagnostics.Metrics;
using OpenCvSharp.Flann;
using System.Windows.Automation.Provider;
using OpenCvSharp.Dnn;
using ZXing.PDF417.Internal;
using System.Net.NetworkInformation;
using static System.Formats.Asn1.AsnWriter;
using Point = OpenCvSharp.Point;
using Newtonsoft.Json.Linq;
using System.Windows.Media.Media3D;
using System.Windows.Media.Animation;
//using ZXing.Presentation;

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
        private Stopwatch m_Stopwatch_fps = new Stopwatch();
        private Stopwatch _stopwatch_getqr = new Stopwatch();

        // weChatQRCode
        //private WeChatQRCode m_WeChatQRCode;
        //[DllImport("kernel32.dll")]

        //private static extern bool AllocConsole(); // 콘솔 할당 함수




        // 펨토메가 관련 코드
        private Pipeline m_pipeline;
        private StreamProfile m_colorProfile;
        //private StreamProfile m_depthProfile;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        private const string detectorPrototxtPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.prototxt";
        private const string detectorCaffeModelPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.caffemodel";
        private const string superResolutionPrototxtPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.prototxt";
        private const string superResolutionCaffeModelPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.caffemodel";
        private WeChatQRCode weChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);
        //private BarcodeReader barcodeReader = new BarcodeReader();

        int width = 3840; // 카메라 해상도 (x 방향)
        int height = 2160; // 카메라 해상도 (y 방향)

        float cx = 326.956f; // 주점 x 위치
        float cy = 315.47f; // 주점 y 위치
        float fx = 504.852f; // 초점 거리 (x 방향)
        float fy = 504.972f; // 초점 거리 (y 방향)

        Mat blurredImage = new Mat();
        private List<double> _listTopResultY = new List<double>();
        private List<double> _listBotResultY = new List<double>();

        private OpenCvSharp.Rect _detectedQRRect;

        //private byte[] resultFrame;
        private string resultQR;
        private int resultHeight;
        private int resultWidth;
        private int resultDepth;

        Vec3f checkDepthVal;
        float checkZValue;


        public HIKVISION(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
        {
            _eventAggregator = eventAggregator;
            _visioncammodel = visioncammodel;
            visioncamConfig = (VisionCamConfigModel)_visioncammodel;
            //AllocConsole();
        }

        public void Init()
        {
            if (visioncamConfig.vision_enable == 0)
            {
                return;
            }

            //m_CheckConnTimer = new DispatcherTimer();
            //m_CheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            //m_CheckConnTimer.Tick += new EventHandler(CheckConnectionEvent);
            //m_CheckConnTimer.Start();

            m_GetImageTimer = new DispatcherTimer();
            m_GetImageTimer.Interval = new TimeSpan(0, 0, 0, 0, 33);
            m_GetImageTimer.Tick += new EventHandler(GetFrame);

            //InitializeWeChatQRCode();
            //OpenVisionCam();

            //펨토메가 관련 코드
            //width = width / 2;
            //height = height / 2;
            InitializeMultiStream();
        }


        /// <summary>
        /// 펨토메가 관련 코드
        /// </summary>
        /// <param name="yValues"></param>
        /// <returns></returns>
        private void InitializeMultiStream()
        {
            try
            {
                Context context = new Context();
                context.CreateNetDevice("192.168.1.10", 8090);
                context.EnableNetDeviceEnumeration(true);

                DeviceList deviceList = context.QueryDeviceList();

                m_pipeline = new Pipeline();
                //Pipeline pipelineFemtoW = new Pipeline();

                for (uint i = 0; i < deviceList.DeviceCount(); i++)
                {
                    Device device = deviceList.GetDevice(i);
                    DeviceInfo deviceInfo = device.GetDeviceInfo();

                    Console.WriteLine("deviceInfo.Name() : " + deviceInfo.Name());
                    //if (deviceInfo.Name().Contains("Femto W"))
                    //{
                    //    pipelineFemtoW = new Pipeline(device);
                    //}
                    if (deviceInfo.Name().Contains("Femto Mega"))
                    {
                        m_pipeline = new Pipeline(device);
                    }
                }

                //m_pipeline = new Pipeline();
                m_colorProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(1920, 1080, Format.OB_FORMAT_RGB, 30);
                //m_colorProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(3840, 2160, Format.OB_FORMAT_RGB, 25);
                //m_colorProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(3840, 2160, Format.OB_FORMAT_H264, 25);
                //m_colorProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(width, height, Format.OB_FORMAT_RGB, 25);
                //m_depthProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH).GetVideoStreamProfile(640, 576, Format.OB_FORMAT_Y16, 25);

                Config config = new Config();
                config.EnableStream(m_colorProfile);
                //config.EnableStream(m_depthProfile);

                m_pipeline.Start(config);

                JObject point = new JObject();
                //JObject points = new JObject();
                JArray points = new JArray();

                // 가로 방향으로 화면을 3등분
                //int sectionWidth = width / 3;

                Task.Factory.StartNew(() => {
                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        using (var frames = m_pipeline.WaitForFrames(100))
                        {
                            var colorFrame = frames?.GetColorFrame();
                            //var depthFrame = frames?.GetDepthFrame();


                            if (colorFrame != null)
                            {
                                int colorWidth = (int)colorFrame.GetWidth();
                                int colorHeight = (int)colorFrame.GetHeight();

                                byte[] colorData = new byte[colorFrame.GetDataSize()];
                                colorFrame.CopyData(ref colorData);

                                Mat colorImage = new Mat(colorHeight, colorWidth, MatType.CV_8UC3);
                                Marshal.Copy(colorData, 0, colorImage.Data, colorData.Length);

                                Cv2.CvtColor(colorImage, colorImage, ColorConversionCodes.BGR2RGB);
                                Cv2.Rotate(colorImage, colorImage, RotateFlags.Rotate90Clockwise);

                                // 3등분한 구역을 설정
                                //OpenCvSharp.Rect roi_top = new OpenCvSharp.Rect(180 * 2, 0, 720 * 2, 480 * 2);
                                //OpenCvSharp.Rect roi_middle = new OpenCvSharp.Rect(0, 0, 2160, 3840);
                                //OpenCvSharp.Rect roi_bottom = new OpenCvSharp.Rect(180 * 2, 1440 * 2, 720 * 2, 1920 * 2);

                                // ROI를 파란색 실선으로 표시
                                //Cv2.Rectangle(colorImage, roi_middle, new Scalar(255, 0, 0), thickness: 4);
                                //Cv2.Rectangle(colorImage, roi_middle, new Scalar(255, 0, 0), thickness: 4);
                                //Cv2.Rectangle(colorImage, roi_top, new Scalar(0, 255, 255), thickness: 4);
                                //Cv2.Rectangle(colorImage, roi_bottom, new Scalar(255, 255, 0), thickness: 4);

                                List<string> qr = new List<string>();
                                //string qrBottom = GetQRcodeIDByWeChat(colorImage, colorWidth, colorHeight, roi_bottom);
                                string qrMiddle = GetQRcodeIDByWeChat(colorImage, colorWidth, colorHeight);
                                //string qrTop = GetQRcodeIDByWeChat(colorImage, colorWidth, colorHeight, roi_top);
                                //if (qrBottom.Contains("wata")) qr.Add(qrBottom);
                                //if (qrMiddle.Contains("wata")) qr.Add(qrMiddle);
                                qr.Add(qrMiddle);
                                //if (qrTop.Contains("wata")) qr.Add(qrTop);

                                //if (qr.Count != 0 && qr.Any(q => q.Contains("wata")))
                                if (qr.Count != 0)
                                {
                                    resultQR = qr[0];
                                    Cv2.Rectangle(colorImage, _detectedQRRect, new Scalar(0, 0, 255), thickness: 4);
                                }

                                Cv2.NamedWindow("COLOR", WindowFlags.KeepRatio);
                                Cv2.ResizeWindow("COLOR", 720, 1280);
                                Cv2.ImShow("COLOR", colorImage);
                                Cv2.WaitKey(1);
                            }

                            //if (depthFrame != null)
                            //{
                            //    //if (!"".Equals(resultQR))
                            //    //{

                            //    //}
                            //    // 포인트 클라우드 데이터
                            //    PointCloudFilter pointCloud = new PointCloudFilter();
                            //    var cameraParam = m_pipeline.GetCameraParam();
                            //    pointCloud.SetCameraParam(cameraParam);
                            //    pointCloud.SetPositionDataScaled(1);

                            //    Orbbec.Frame pointFrame = pointCloud.Process(depthFrame);

                            //    byte[] pointData = new byte[pointFrame.GetDataSize()];
                            //    pointFrame.CopyData(ref pointData);


                            //    int depthWidth = (int)depthFrame.GetWidth();
                            //    int depthHeight = (int)depthFrame.GetHeight();


                            //    Mat depthImage = new Mat(depthHeight, depthWidth, MatType.CV_32FC3);
                            //    Marshal.Copy(pointData, 0, depthImage.Data, pointData.Length);

                            //    Cv2.Rotate(depthImage, depthImage, RotateFlags.Rotate90Counterclockwise);

                            //    Parallel.For(0, depthImage.Rows, i => {
                            //        for (int j = 0; j < depthImage.Cols; j++)
                            //        {
                            //            Vec3f depthVec = depthImage.At<Vec3f>(i, j);
                            //            float zValue = depthVec[2];

                            //            checkDepthVal = depthVec;
                            //            checkZValue = zValue;

                            //            if (zValue < 1200.0f || zValue > 2600.0f || (j >= 0 && j < 120) || (j >= 456 && j <= 576))
                            //            {
                            //                depthImage.Set<Vec3f>(i, j, new Vec3f(0, 0, 0));
                            //            }
                            //        }
                            //    });

                            //    double minYValue, maxYValue;
                            //    Point minYLoc, maxYLoc;

                            //    Mat yChannel = new Mat();
                            //    Cv2.ExtractChannel(depthImage, yChannel, 0);

                            //    Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                            //    Cv2.Erode(yChannel, yChannel, kernel);
                            //    Cv2.Dilate(yChannel, yChannel, kernel);

                            //    Cv2.MinMaxLoc(yChannel, out minYValue, out maxYValue, out minYLoc, out maxYLoc);

                            //    for (int i = 590; i < 630; i++)
                            //    {
                            //        minYValue = yChannel.At<Vec3f>(i, 288)[1];
                            //        //Console.WriteLine("minYValue : " + minYValue);
                            //        if (minYValue < -500)
                            //        {
                            //            minYLoc = new Point(288, i);
                            //            break;
                            //        }
                            //    }


                            //    if (maxYValue == 0)
                            //    {
                            //        double maxNegativeValue = double.MinValue;
                            //        for (int i = 0; i < yChannel.Rows; i++)
                            //        {
                            //            for (int j = 0; j < yChannel.Cols; j++)
                            //            {
                            //                float yValue = yChannel.At<Vec3f>(i, j)[1];
                            //                if (yValue < 0 && yValue > maxNegativeValue)
                            //                {
                            //                    maxNegativeValue = yValue;
                            //                    maxYLoc = new Point(j, i);
                            //                }
                            //            }
                            //        }
                            //        maxYValue = maxNegativeValue;
                            //    }


                            //    Cv2.Circle(depthImage, maxYLoc, 5, new Scalar(150, 150, 150), -1);
                            //    Cv2.Circle(depthImage, minYLoc, 5, new Scalar(150, 150, 150), -1);

                            //    if (maxYValue != 0 && minYValue != 0)
                            //    {
                            //        _listTopResultY.Add(maxYValue);
                            //        _listBotResultY.Add(minYValue);
                            //        Console.WriteLine($"Min = {minYValue}, Max = {maxYValue}");
                            //    }

                            //    if (_listTopResultY.Count == 10 && _listBotResultY.Count == 10) // 속도 빠르면 15, 20 테스트
                            //    {
                            //        int heightOffset = 30;
                            //        int resultTopAvg = CalculateAverage(_listTopResultY);
                            //        int resultBotAvg = CalculateAverage(_listBotResultY);
                            //        int result = (int)(Math.Abs(minYValue) + maxYValue - heightOffset);

                            //        _listTopResultY.Clear();
                            //        _listBotResultY.Clear();
                            //        resultQR = "";
                            //        resultHeight = result;

                            //        //Mat resizedImage = new Mat();
                            //        //Cv2.Resize(depthImage, resizedImage, new OpenCvSharp.Size(depthWidth / 12, depthHeight / 12));

                            //        //for (int y = 0; y < resizedImage.Rows; y++)
                            //        //{
                            //        //    for (int x = 0; x < resizedImage.Cols; x++)
                            //        //    {
                            //        //        Vec3f pt = resizedImage.At<Vec3f>(y, x);
                            //        //        if (pt.Item2 > 0 && (pt.Item0 != pt.Item2))
                            //        //        {
                            //        //            point["x"] = pt.Item0;
                            //        //            point["y"] = pt.Item1;
                            //        //            point["z"] = pt.Item2;
                            //        //            points.Add(point);
                            //        //            //Console.WriteLine($"{pt[0]} {pt[1]} {pt[2]}");
                            //        //        }
                            //        //    }
                            //        //}

                            //        Console.WriteLine("---------------------------------------------");
                            //        Console.WriteLine($"Height : {result} mm");
                            //        Console.WriteLine("---------------------------------------------");
                            //    }

                            //    Cv2.NamedWindow("depthImage", WindowFlags.KeepRatio);
                            //    Cv2.ResizeWindow("depthImage", 640, 576);
                            //    Cv2.ImShow("depthImage", depthImage);
                            //    Cv2.WaitKey(1);

                            //}
                        }

                        // Publish the event
                        VisionCamModel eventModels = new VisionCamModel();
                        eventModels.QR = resultQR == null ? "" : resultQR;
                        eventModels.WIDTH = 0;
                        eventModels.HEIGHT = resultHeight;
                        eventModels.ACTION_DEPTH = checkZValue;
                        eventModels.POINTS = points.ToString();
                        eventModels.connected = true;
                        _eventAggregator.GetEvent<VisionCamEvent>().Publish(eventModels);
                    }
                }, tokenSource.Token);
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                Application.Current.Shutdown();
                m_pipeline = null;
                m_colorProfile = null;
                //m_depthProfile = null;
            }
        }

        //public int CalculateAverage(List<double> yValues)
        //{
        //    var sortedYValues = yValues.OrderBy(y => y).ToList();
        //    int count = sortedYValues.Count;
        //    int removeCount = (int)(count * 0.2);
        //    var trimmedYValues = sortedYValues.Skip(removeCount).Take(count - 2 * removeCount).ToList();

        //    return (int)trimmedYValues.Average();
        //}

        private string GetQRcodeIDByWeChat(Mat frame, int colorWidth, int colorHeight)
        {
            string ret = string.Empty;

            weChatQRCode.DetectAndDecode(frame, out Mat[] bbox, out string[] weChatResult);

            Console.WriteLine("colorWidth : " + colorWidth + " bbox : " + bbox.Length);
            if (weChatResult != null && weChatResult.Length > 0)
            {
                double closestDistance = double.MaxValue;
                int cnt = 0;
                foreach (var box in bbox)
                {
                    OpenCvSharp.Point[] detectedQRpoints = new OpenCvSharp.Point[4];

                    //if (box.Total() >= 4 && weChatResult[cnt].Contains("wata"))
                    if (box.Total() >= 4)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            detectedQRpoints[j] = new OpenCvSharp.Point((int)box.At<float>(j, 0), (int)box.At<float>(j, 1));
                        }

                        int minX = detectedQRpoints.Min(p => p.X);
                        int minY = detectedQRpoints.Min(p => p.Y);
                        int maxX = detectedQRpoints.Max(p => p.X);
                        int maxY = detectedQRpoints.Max(p => p.Y);

                        var qrRect = new OpenCvSharp.Rect(minX, minY, maxX - minX, maxY - minY);
                        int centerX = qrRect.X + qrRect.Width / 2;
                        int centerY = qrRect.Y + qrRect.Height / 2;

                        double distance = Math.Abs(centerX - colorHeight / 2);

                        Cv2.Rectangle(frame, qrRect, new Scalar(0, 255, 0), thickness: 4);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            _detectedQRRect = qrRect;
                            ret = weChatResult[cnt];
                        }

                        //// Check if the QR code is within the ROI
                        //if (roi.Contains(new OpenCvSharp.Point(centerX, centerY)))
                        //{
                        //    double distance = Math.Abs(centerX - colorHeight / 2);

                        //    Cv2.Rectangle(frame, qrRect, new Scalar(0, 255, 0), thickness: 4);
                        //    if (distance < closestDistance)
                        //    {
                        //        closestDistance = distance;
                        //        _detectedQRRect = qrRect;
                        //        ret = weChatResult[cnt];
                        //    }
                        //}



                    }
                    cnt++;
                }
            }

            //var zXingResult = barcodeReader.Decode(frame.ToBitmap());

            //if (zXingResult != null && zXingResult.ResultPoints.Length > 4 && zXingResult.Text.Contains("wata"))
            //{
            //    int x1 = (int)zXingResult.ResultPoints[0].X;
            //    int y1 = (int)zXingResult.ResultPoints[0].Y;
            //    int x2 = (int)zXingResult.ResultPoints[1].X;
            //    int y2 = (int)zXingResult.ResultPoints[1].Y;
            //    int x3 = (int)zXingResult.ResultPoints[2].X;
            //    int y3 = (int)zXingResult.ResultPoints[2].Y;
            //    int x4 = (int)zXingResult.ResultPoints[3].X;
            //    int y4 = (int)zXingResult.ResultPoints[3].Y;

            //    int minX = Math.Min(Math.Min(x1, x2), Math.Min(x3, x4));
            //    int minY = Math.Min(Math.Min(y1, y2), Math.Min(y3, y4));
            //    int maxX = Math.Max(Math.Max(x1, x2), Math.Max(x3, x4));
            //    int maxY = Math.Max(Math.Max(y1, y2), Math.Max(y3, y4));

            //    _detectedQRRect = new OpenCvSharp.Rect(minX, minY, maxX - minX, maxY - minY);
            //    ret = zXingResult.Text;
            //}

            return ret;
        }

        private void CheckConnectionEvent(object sender, EventArgs e)
        {
            if (m_pipeline == null)
            {
                Tools.Log("VisionCam is disconnected", Tools.ELogType.SystemLog);
                InitializeMultiStream();
                return;
            }
            else
            {
                //Tools.Log("VisionCam is alive", Tools.ELogType.SystemLog);
            }
        }












        /// <summary>
        /// HIKvision 관련 코드
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void InitializeWeChatQRCode()
        {
            //string exePath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            ////string detectorPrototxtPath = $"{exePath}\\sr.prototxt";
            ////string detectorCaffeModelPath = $"{exePath}\\detect.caffemodel";
            ////string superResolutionPrototxtPath = $"{exePath}\\sr.prototxt";
            ////string superResolutionCaffeModelPath = $"{exePath}\\sr.caffemodel";

            //string detectorPrototxtPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.prototxt";
            //string detectorCaffeModelPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.caffemodel";
            //string superResolutionPrototxtPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.prototxt";
            //string superResolutionCaffeModelPath = @"C:\Users\wmszz\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.caffemodel";

            //// 모델 파일 경로 확인
            //if (!File.Exists(detectorPrototxtPath) || !File.Exists(detectorCaffeModelPath) ||
            //    !File.Exists(superResolutionPrototxtPath) || !File.Exists(superResolutionCaffeModelPath))
            //{
            //    throw new FileNotFoundException("One or more WeChatQRCode model files are missing.");
            //}

            //m_WeChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);

        }

        private async void OpenVisionCam()
        {
            try
            {
                Tools.Log("VisionCam Initiating...", Tools.ELogType.SystemLog);
                await Task.Run(() => {
                    // 카메라의 RTSP URL 설정
                    string rtspUrl = $"rtsp://{visioncamConfig.vision_id}:{visioncamConfig.vision_pw}@{visioncamConfig.vision_ip}:554/Stream/Channels/101?transportmode=unicast";
                    m_Capture = new VideoCapture(rtspUrl);
                    //if (m_Capture != null) m_Capture.Dispose();

                    //m_CameraIndex = 0;
                    //m_Capture = new VideoCapture(m_CameraIndex);
                    //m_Capture.FrameWidth = 3840;
                    //m_Capture.FrameHeight = 2160;
                    //m_Capture.Set(VideoCaptureProperties.Fps, 30); // 초당 프레임 설정
                    //m_Capture.Set(VideoCaptureProperties.Focus, 1.5); // 카메라 초점거리 설정

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

        private void GetFrame(object sender, EventArgs e)
        {
            try
            {
                m_MatImage = new Mat();
                m_Stopwatch_fps.Restart();
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
                m_Stopwatch_fps.Stop();
                double elapsedTime_fps = m_Stopwatch_fps.Elapsed.TotalSeconds;
                double _fps = 1.0 / elapsedTime_fps;

                _stopwatch_getqr.Restart();
                m_LastQRCode = GetQRcode(m_MatImage, 100);

                // Get QR Code 계산
                _stopwatch_getqr.Stop();
                double _getqr = _stopwatch_getqr.Elapsed.TotalMilliseconds;

                // Resize the frame
                Mat resizedFrame = new Mat();
                Cv2.Resize(m_MatImage, resizedFrame, new OpenCvSharp.Size(640, 480));
                //Cv2.Rotate(m_MatImage, resizedFrame, RotateFlags.Rotate90Clockwise);

                // FPS 정보, GET QR 소요시간 프레임에 오버레이
                //Cv2.PutText(m_MatImage, $"FPS: {_fps:F2}", new OpenCvSharp.Point(150, 300), HersheyFonts.HersheySimplex, 10, Scalar.Red, 20);
                //Cv2.PutText(m_MatImage, $"GET QR: {_getqr:F2} ms", new OpenCvSharp.Point(150, 550), HersheyFonts.HersheySimplex, 10, Scalar.Red, 20);
                Cv2.PutText(resizedFrame, $"FPS: {_fps:F2}", new OpenCvSharp.Point(10, 30), HersheyFonts.HersheySimplex, 1, Scalar.Red, 2);
                Cv2.PutText(resizedFrame, $"GET QR: {_getqr:F2} ms", new OpenCvSharp.Point(10, 60), HersheyFonts.HersheySimplex, 1, Scalar.Red, 2);

                byte[] currentFrameBytes = resizedFrame.ToBytes();

                // Publish the event
                VisionCamModel eventModels = new VisionCamModel();
                eventModels.QR = m_LastQRCode;
                eventModels.FRAME = currentFrameBytes;
                eventModels.connected = true;

                _eventAggregator.GetEvent<VisionCamEvent>().Publish(eventModels);
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
                        weChatQRCode.DetectAndDecode(frame, out Mat[] bbox, out string[] results);

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