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
    public class FEMTOMEGA : System.Windows.Window
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
        private List<double> _listTopResultY = new List<double>();
        private List<double> _listBotResultY = new List<double>();
        private Stopwatch m_Stopwatch_fps = new Stopwatch();
        private Stopwatch _stopwatch_getqr = new Stopwatch();

        // weChatQRCode
        //private WeChatQRCode m_WeChatQRCode;


        //[DllImport("kernel32.dll")]
        //private static extern bool AllocConsole(); // 콘솔 할당 함수




        // 펨토메가 관련 코드
        private Pipeline m_pipeline;
        //private Pipeline m_pipelineDepth;
        private StreamProfile m_colorProfile;
        private StreamProfile m_depthProfile;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        private const string detectorPrototxtPath = @"C:\Users\USER\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.prototxt";
        private const string detectorCaffeModelPath = @"C:\Users\USER\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\detect.caffemodel";
        private const string superResolutionPrototxtPath = @"C:\Users\USER\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.prototxt";
        private const string superResolutionCaffeModelPath = @"C:\Users\USER\source\repos\LIS-ForkLift_mswon\WATA.LIS\Modules\WATA.LIS.VISION.CAM\Model\sr.caffemodel";
        private WeChatQRCode weChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);
        //private BarcodeReader barcodeReader = new BarcodeReader();
        private BarcodeReader<Bitmap> barcodeReader = new BarcodeReader<Bitmap>(null, null, null);

        private OpenCvSharp.Rect _detectedQRRect;

        private string resultQR;
        private int resultHeight;
        private int resultWidth;
        private int resultDepth;

        Vec3f checkDepthVal;
        float checkZValue;

        private bool previousState = false; // 이전 상태를 저장하는 변수
        private double previousScale = 0.0; // 이전 프레임의 스케일 값을 저장하는 변수



        public FEMTOMEGA(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
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

            //InitializeWeChatQRCode();

            //펨토메가 관련 코드
            InitializeMultiStream();
        }

        private void InitializeMultiStream()
        {
            try
            {
                Context context = new Context();
                context.CreateNetDevice(visioncamConfig.vision_ip, 8090);
                context.EnableNetDeviceEnumeration(true);

                DeviceList deviceList = context.QueryDeviceList();

                m_pipeline = new Pipeline();

                for (uint i = 0; i < deviceList.DeviceCount(); i++)
                {
                    Device device = deviceList.GetDevice(i);
                    DeviceInfo deviceInfo = device.GetDeviceInfo();

                    Console.WriteLine("deviceInfo.Name() : " + deviceInfo.Name());
                    if (deviceInfo.Name().Contains("Femto Mega"))
                    {
                        m_pipeline = new Pipeline(device);
                    }
                }

                m_colorProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(1920, 1080, Format.OB_FORMAT_RGB, 25);
                m_depthProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH).GetVideoStreamProfile(640, 576, Format.OB_FORMAT_Y16, 25);

                Config config = new Config();
                config.EnableStream(m_colorProfile);
                config.EnableStream(m_depthProfile);

                m_pipeline.Start(config);

                JObject point = new JObject();
                JArray points = new JArray();

                Task.Factory.StartNew(() => {
                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        using (var frames = m_pipeline.WaitForFrames(100))
                        {
                            var colorFrame = frames?.GetColorFrame();
                            var depthFrame = frames?.GetDepthFrame();

                            if (colorFrame == null || depthFrame == null)
                            {
                                continue;
                            }

                            // RGB 프레임 중 QR 처리
                            int colorWidth = (int)colorFrame.GetWidth();
                            int colorHeight = (int)colorFrame.GetHeight();

                            byte[] colorData = new byte[colorFrame.GetDataSize()];
                            colorFrame.CopyData(ref colorData);

                            Mat colorImage = new Mat(colorHeight, colorWidth, MatType.CV_8UC3);
                            Marshal.Copy(colorData, 0, colorImage.Data, colorData.Length);

                            string detectQR = GetQRcodeIDByWeChat(colorImage, colorWidth, colorHeight);

                            // Depth 프레임 중 AvgDepth 처리
                            int depthWidth = (int)depthFrame.GetWidth();
                            int depthHeight = (int)depthFrame.GetHeight();

                            byte[] depthData = new byte[depthFrame.GetDataSize()];
                            depthFrame.CopyData(ref depthData);

                            Mat depthImage = new Mat(depthHeight, depthWidth, MatType.CV_16UC1);
                            Marshal.Copy(depthData, 0, depthImage.Data, depthData.Length);

                            // Depth 이미지를 colorFrame 해상도에 맞게 조정
                            Mat resizedDepthImage = new Mat();
                            Cv2.Resize(depthImage, resizedDepthImage, new OpenCvSharp.Size(colorWidth, colorHeight), 0, 0, InterpolationFlags.Nearest);

                            // 화면 좌측 중앙 부분의 Depth 값 가져오기
                            int regionSize = 300;
                            int halfRegion = regionSize / 2;
                            int centerX = halfRegion; // 화면 좌측 끝에 맞추기
                            int centerY = colorHeight / 2;
                            int sumDepth = 0;
                            int count = 0;

                            for (int y = centerY - halfRegion; y <= centerY + halfRegion; y++)
                            {
                                for (int x = 0; x <= regionSize; x++) // 좌측 끝에서 시작
                                {
                                    if (x >= 0 && x < colorWidth && y >= 0 && y < colorHeight)
                                    {
                                        sumDepth += resizedDepthImage.At<ushort>(y, x);
                                        count++;
                                    }
                                }
                            }

                            ushort averageDepth = (ushort)(sumDepth / count);

                            // centerX, centerY를 중심으로 하는 네모 박스 그리기
                            Cv2.Rectangle(colorImage, new Point(0, centerY - halfRegion), new Point(regionSize, centerY + halfRegion), new Scalar(255, 0, 255), thickness: 4);


                            // 화면 회전 및 RGB 변환
                            Cv2.CvtColor(colorImage, colorImage, ColorConversionCodes.BGR2RGB);
                            Cv2.Rotate(colorImage, colorImage, RotateFlags.Rotate90Counterclockwise);


                            // Convert Mat to byte array
                            byte[] frameData;
                            using (var ms = new MemoryStream())
                            {
                                colorImage.WriteToStream(ms);
                                frameData = ms.ToArray();
                            }


                            // Publish the event
                            VisionCamModel eventModels = new VisionCamModel();
                            eventModels.QR = detectQR == null ? "" : detectQR;
                            eventModels.PIKCUP_DEPTH = averageDepth;
                            eventModels.POINTS = points.ToString();
                            eventModels.FRAME = frameData;
                            eventModels.connected = true;
                            _eventAggregator.GetEvent<HikVisionEvent>().Publish(eventModels);
                        }
                    }
                }, tokenSource.Token);
            }
            catch (Exception e)
            {
                Application.Current.Shutdown();
                m_pipeline = null;
                m_colorProfile = null;
            }
        }

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

        private void InitializeMultiStream_Old()
        {
            try
            {
                Context context = new Context();
                context.CreateNetDevice(visioncamConfig.vision_ip, 8090);
                context.EnableNetDeviceEnumeration(true);

                DeviceList deviceList = context.QueryDeviceList();

                m_pipeline = new Pipeline();

                for (uint i = 0; i < deviceList.DeviceCount(); i++)
                {
                    Device device = deviceList.GetDevice(i);
                    DeviceInfo deviceInfo = device.GetDeviceInfo();

                    Console.WriteLine("deviceInfo.Name() : " + deviceInfo.Name());

                    if (deviceInfo.Name().Contains("Femto Mega"))
                    {
                        m_pipeline = new Pipeline(device);
                    }
                }

                m_colorProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(1920, 1080, Format.OB_FORMAT_RGB, 30);
                m_depthProfile = m_pipeline.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH).GetVideoStreamProfile(640, 576, Format.OB_FORMAT_Y16, 25);

                Config config = new Config();
                config.EnableStream(m_colorProfile);
                config.EnableStream(m_depthProfile);

                m_pipeline.Start(config);

                JObject point = new JObject();
                JArray points = new JArray();

                Task.Factory.StartNew(() => {
                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        using (var frames = m_pipeline.WaitForFrames(100))
                        {
                            var colorFrame = frames?.GetColorFrame();
                            var depthFrame = frames?.GetDepthFrame();


                            // RGB 데이터
                            if (colorFrame == null || depthFrame == null)
                            {
                                continue;
                            }

                            int colorWidth = (int)colorFrame.GetWidth();
                            int colorHeight = (int)colorFrame.GetHeight();

                            byte[] colorData = new byte[colorFrame.GetDataSize()];
                            colorFrame.CopyData(ref colorData);

                            Mat colorImage = new Mat(colorHeight, colorWidth, MatType.CV_8UC3);
                            Marshal.Copy(colorData, 0, colorImage.Data, colorData.Length);

                            Cv2.CvtColor(colorImage, colorImage, ColorConversionCodes.BGR2RGB);
                            Cv2.Rotate(colorImage, colorImage, RotateFlags.Rotate90Counterclockwise);

                            List<string> qr = new List<string>();
                            string qrMiddle = GetQRcodeIDByWeChat(colorImage, colorWidth, colorHeight);
                            qr.Add(qrMiddle);

                            if (qr.Count != 0)
                            {
                                resultQR = qr[0];
                                Cv2.Rectangle(colorImage, _detectedQRRect, new Scalar(0, 0, 255), thickness: 4);
                            }


                            //// 포인트 클라우드 데이터
                            //PointCloudFilter pointCloud = new PointCloudFilter();
                            //var cameraParam = m_pipeline.GetCameraParam();
                            //pointCloud.SetCameraParam(cameraParam);
                            //pointCloud.SetPositionDataScaled(1);

                            //Orbbec.Frame pointFrame = pointCloud.Process(depthFrame);

                            //byte[] pointData = new byte[pointFrame.GetDataSize()];
                            //pointFrame.CopyData(ref pointData);


                            //int depthWidth = (int)depthFrame.GetWidth();
                            //int depthHeight = (int)depthFrame.GetHeight();


                            //Mat depthImage = new Mat(depthHeight, depthWidth, MatType.CV_32FC3);
                            //Marshal.Copy(pointData, 0, depthImage.Data, pointData.Length);

                            //Cv2.Rotate(depthImage, depthImage, RotateFlags.Rotate90Counterclockwise);

                            //Parallel.For(0, depthImage.Rows, i => {
                            //    for (int j = 0; j < depthImage.Cols; j++)
                            //    {
                            //        Vec3f depthVec = depthImage.At<Vec3f>(i, j);
                            //        float zValue = depthVec[2];

                            //        checkDepthVal = depthVec;
                            //        checkZValue = zValue;

                            //        if (zValue < 1200.0f || zValue > 2600.0f || (j >= 0 && j < 120) || (j >= 456 && j <= 576))
                            //        {
                            //            depthImage.Set<Vec3f>(i, j, new Vec3f(0, 0, 0));
                            //        }
                            //    }
                            //});

                            //double minYValue, maxYValue;
                            //Point minYLoc, maxYLoc;

                            //Mat yChannel = new Mat();
                            //Cv2.ExtractChannel(depthImage, yChannel, 0);

                            //Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                            //Cv2.Erode(yChannel, yChannel, kernel);
                            //Cv2.Dilate(yChannel, yChannel, kernel);

                            //Cv2.MinMaxLoc(yChannel, out minYValue, out maxYValue, out minYLoc, out maxYLoc);

                            //for (int i = 590; i < 630; i++)
                            //{
                            //    minYValue = yChannel.At<Vec3f>(i, 288)[1];
                            //    if (minYValue < -500)
                            //    {
                            //        minYLoc = new Point(288, i);
                            //        break;
                            //    }
                            //}


                            //if (maxYValue == 0)
                            //{
                            //    double maxNegativeValue = double.MinValue;
                            //    for (int i = 0; i < yChannel.Rows; i++)
                            //    {
                            //        for (int j = 0; j < yChannel.Cols; j++)
                            //        {
                            //            float yValue = yChannel.At<Vec3f>(i, j)[1];
                            //            if (yValue < 0 && yValue > maxNegativeValue)
                            //            {
                            //                maxNegativeValue = yValue;
                            //                maxYLoc = new Point(j, i);
                            //            }
                            //        }
                            //    }
                            //    maxYValue = maxNegativeValue;
                            //}


                            //Cv2.Circle(depthImage, maxYLoc, 5, new Scalar(150, 150, 150), -1);
                            //Cv2.Circle(depthImage, minYLoc, 5, new Scalar(150, 150, 150), -1);

                            //if (maxYValue != 0 && minYValue != 0)
                            //{
                            //    _listTopResultY.Add(maxYValue);
                            //    _listBotResultY.Add(minYValue);
                            //    Console.WriteLine($"Min = {minYValue}, Max = {maxYValue}");
                            //}

                            //if (_listTopResultY.Count == 10 && _listBotResultY.Count == 10) // 속도 빠르면 15, 20 테스트
                            //{
                            //    int heightOffset = 30;
                            //    int resultTopAvg = CalculateAverage(_listTopResultY);
                            //    int resultBotAvg = CalculateAverage(_listBotResultY);
                            //    int result = (int)(Math.Abs(minYValue) + maxYValue - heightOffset);

                            //    _listTopResultY.Clear();
                            //    _listBotResultY.Clear();
                            //    resultQR = "";
                            //    resultHeight = result;

                            //    Mat resizedImage = new Mat();
                            //    Cv2.Resize(depthImage, resizedImage, new OpenCvSharp.Size(depthWidth / 12, depthHeight / 12));

                            //    for (int y = 0; y < resizedImage.Rows; y++)
                            //    {
                            //        for (int x = 0; x < resizedImage.Cols; x++)
                            //        {
                            //            Vec3f pt = resizedImage.At<Vec3f>(y, x);
                            //            if (pt.Item2 > 0 && (pt.Item0 != pt.Item2))
                            //            {
                            //                point["x"] = pt.Item0;
                            //                point["y"] = pt.Item1;
                            //                point["z"] = pt.Item2;
                            //                points.Add(point);
                            //                //Console.WriteLine($"{pt[0]} {pt[1]} {pt[2]}");
                            //            }
                            //        }
                            //    }

                            //    Console.WriteLine("---------------------------------------------");
                            //    Console.WriteLine($"Height : {result} mm");
                            //    Console.WriteLine("---------------------------------------------");
                            //}

                            //Cv2.NamedWindow("depthImage", WindowFlags.KeepRatio);
                            //Cv2.ResizeWindow("depthImage", 640, 576);
                            //Cv2.ImShow("depthImage", depthImage);
                            //Cv2.WaitKey(1);



                            // Convert Mat to byte array
                            byte[] frameData;
                            using (var ms = new MemoryStream())
                            {
                                colorImage.WriteToStream(ms);
                                frameData = ms.ToArray();
                            }

                            // Publish the event
                            VisionCamModel eventModels = new VisionCamModel();
                            eventModels.QR = resultQR == null ? "" : resultQR;
                            eventModels.POINTS = points.ToString();
                            eventModels.FRAME = frameData;
                            eventModels.connected = true;
                            _eventAggregator.GetEvent<HikVisionEvent>().Publish(eventModels);
                        }
                    }
                }, tokenSource.Token);
            }
            catch (Exception e)
            {
                Application.Current.Shutdown();
                m_pipeline = null;
                m_colorProfile = null;
            }
        }

        public int CalculateAverage(List<double> yValues)
        {
            var sortedYValues = yValues.OrderBy(y => y).ToList();
            int count = sortedYValues.Count;
            int removeCount = (int)(count * 0.2);
            var trimmedYValues = sortedYValues.Skip(removeCount).Take(count - 2 * removeCount).ToList();

            return (int)trimmedYValues.Average();
        }
    }
}