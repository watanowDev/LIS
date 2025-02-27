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
using System.Net.NetworkInformation;
using static System.Formats.Asn1.AsnWriter;
using Point = OpenCvSharp.Point;
using Newtonsoft.Json.Linq;
using System.Windows.Media.Media3D;
using System.Windows.Media.Animation;
using System.Drawing.Imaging;
using ZXing;
using System.ComponentModel;


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
        private int noImageCnt = 0;

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

        private readonly static string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly static string projectRootDirectory = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(baseDirectory).FullName).FullName).FullName).FullName).FullName;
        private readonly static string modelDirectory = Path.Combine(projectRootDirectory, "Modules", "WATA.LIS.VISION.CAM", "Model");
        private readonly static string detectorPrototxtPath = Path.Combine(modelDirectory, "detect.prototxt");
        private readonly static string detectorCaffeModelPath = Path.Combine(modelDirectory, "detect.caffemodel");
        private readonly static string superResolutionPrototxtPath = Path.Combine(modelDirectory, "sr.prototxt");
        private readonly static string superResolutionCaffeModelPath = Path.Combine(modelDirectory, "sr.caffemodel");
        private WeChatQRCode weChatQRCode = WeChatQRCode.Create(detectorPrototxtPath, detectorCaffeModelPath, superResolutionPrototxtPath, superResolutionCaffeModelPath);

        //private BarcodeReader barcodeReader = new BarcodeReader();
        //private BarcodeReader<Bitmap> barcodeReader = new BarcodeReader<Bitmap>(null, null, null);

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

            // 창의 Closing 이벤트에 핸들러 추가
            this.Closing += OnWindowClosing;
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // 확인 창을 생략하고 바로 종료
            Application.Current.Shutdown();
        }

        public void Init()
        {
            if (visioncamConfig.vision_enable == 0)
            {
                return;
            }

            m_CheckConnTimer = new DispatcherTimer();
            m_CheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            m_CheckConnTimer.Tick += new EventHandler(CheckConnectionEvent);
            m_CheckConnTimer.Start();

            m_GetImageTimer = new DispatcherTimer();
            m_GetImageTimer.Interval = new TimeSpan(0, 0, 0, 0, 33);

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
                    // 거꾸로된 다이아몬드 모양의 ROI 설정 (depth 카메라 해상도 기준)
                    List<(string, OpenCvSharp.Rect)> rois = new List<(string, OpenCvSharp.Rect)>
                        {
                            ("TM", new OpenCvSharp.Rect(520, 313, 50, 50)),
                            ("MR", new OpenCvSharp.Rect(345, 463, 50, 50)),
                            ("MM", new OpenCvSharp.Rect(345, 313, 50, 50)),
                            ("ML", new OpenCvSharp.Rect(345, 163, 50, 50)),
                            ("BR", new OpenCvSharp.Rect(120, 438, 50, 50)),
                            ("BM", new OpenCvSharp.Rect(120, 313, 50, 50)),
                            ("BL", new OpenCvSharp.Rect(120, 188, 50, 50))
                        };

                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        using (var frames = m_pipeline.WaitForFrames(100))
                        {
                            var colorFrame = frames?.GetColorFrame();
                            var depthFrame = frames?.GetDepthFrame();

                            if (colorFrame == null || depthFrame == null)
                            {
                                noImageCnt++;
                                if (noImageCnt >= 250 && noImageCnt % 5 == 0)
                                {
                                    m_pipeline = null;
                                    m_colorProfile = null;
                                    CheckConnectionEvent(null, null);
                                }
                                continue;
                            }
                            noImageCnt = 0;

                            // 이벤트 데이터 객체 생성
                            VisionCamModel eventModels = new VisionCamModel();

                            // RGB 프레임 객체 생성
                            int colorWidth = (int)colorFrame.GetWidth();
                            int colorHeight = (int)colorFrame.GetHeight();

                            byte[] colorData = new byte[colorFrame.GetDataSize()];
                            colorFrame.CopyData(ref colorData);

                            Mat colorImage = new Mat(colorHeight, colorWidth, MatType.CV_8UC3);
                            Marshal.Copy(colorData, 0, colorImage.Data, colorData.Length);
                            Cv2.CvtColor(colorImage, colorImage, ColorConversionCodes.BGR2RGB);

                            // Depth 프레임 객체 생성
                            int depthWidth = (int)depthFrame.GetWidth();
                            int depthHeight = (int)depthFrame.GetHeight();

                            byte[] depthData = new byte[depthFrame.GetDataSize()];
                            depthFrame.CopyData(ref depthData);

                            Mat depthImage = new Mat(depthHeight, depthWidth, MatType.CV_16UC1);
                            Marshal.Copy(depthData, 0, depthImage.Data, depthData.Length);

                            // RGB QR 처리
                            string detectQR = GetQRcodeIDByWeChat(colorImage, colorWidth, colorHeight);
                            eventModels.QR = detectQR == null ? "" : detectQR;

                            List<(string, double)> roiResults = new List<(string, double)>();

                            foreach (var (roiName, roi) in rois)
                            {
                                // Depth ROI에서 평균값 계산
                                Mat roiDepthImage = new Mat(depthImage, roi);
                                Scalar depthSumScalar = Cv2.Sum(roiDepthImage);
                                double depthSum = depthSumScalar.Val0;
                                double depthAverage = depthSum / (roi.Width * roi.Height);

                                switch (roiName)
                                {
                                    case "TM":
                                        eventModels.TM_DEPTH = depthAverage;
                                        break;
                                    case "MR":
                                        eventModels.MR_DEPTH = depthAverage;
                                        break;
                                    case "MM":
                                        eventModels.MM_DEPTH = depthAverage;
                                        break;
                                    case "ML":
                                        eventModels.ML_DEPTH = depthAverage;
                                        break;
                                    case "BR":
                                        eventModels.BR_DEPTH = depthAverage;
                                        break;
                                    case "BM":
                                        eventModels.BM_DEPTH = depthAverage;
                                        break;
                                    case "BL":
                                        eventModels.BL_DEPTH = depthAverage;
                                        break;
                                }
                            }

                            // RGB 프레임 반시계방향으로 90도 회전
                            Cv2.Rotate(colorImage, colorImage, RotateFlags.Rotate90Counterclockwise);

                            // Convert Mat to byte array
                            byte[] frameData;
                            using (var ms = new MemoryStream())
                            {
                                colorImage.WriteToStream(ms);
                                frameData = ms.ToArray();
                            }

                            eventModels.FRAME = frameData;
                            eventModels.connected = true;
                            _eventAggregator.GetEvent<HikVisionEvent>().Publish(eventModels);
                        }
                    }
                }, tokenSource.Token);

                Tools.Log("VisionCam Init succeeded", Tools.ELogType.SystemLog);
            }
            catch
            {
                m_pipeline = null;
                m_colorProfile = null;
                Application.Current.Shutdown();
            }
        }

        private string GetQRcodeIDByWeChat(Mat frame, int colorWidth, int colorHeight)
        {
            string ret = string.Empty;

            // WeChat QR 코드 인식
            weChatQRCode.DetectAndDecode(frame, out Mat[] bbox, out string[] weChatResult);

            Console.WriteLine("colorWidth : " + colorWidth + " bbox : " + bbox.Length);
            if (weChatResult != null && weChatResult.Length > 0)
            {
                double closestDistance = double.MaxValue;
                int cnt = 0;
                foreach (var box in bbox)
                {
                    OpenCvSharp.Point[] detectedQRpoints = new OpenCvSharp.Point[4];

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

            //if (ret == string.Empty)
            //{
            //    Bitmap pImg = MakeGrayscale3(frame.ToBitmap());
            //    using (ZBar.ImageScanner scanner = new ZBar.ImageScanner())
            //    {
            //        //scanner.SetConfiguration(ZBar.SymbolType.None, ZBar.Config.Enable, 0);
            //        //scanner.SetConfiguration(ZBar.SymbolType.CODE39, ZBar.Config.Enable, 1);
            //        //scanner.SetConfiguration(ZBar.SymbolType.CODE128, ZBar.Config.Enable, 1);
            //        //scanner.SetConfiguration(ZBar.SymbolType.QRCODE, ZBar.Config.Enable, 1);

            //        List<ZBar.Symbol> symbols = new List<ZBar.Symbol>();
            //        symbols = scanner.Scan(pImg);
            //        pImg.Dispose();
            //        ret = symbols.Count > 0 ? symbols[0].Data : "Barcode Fail";
            //    }
            //}

            //if (ret == string.Empty)
            //{
            //    ZXing.BarcodeReaderGeneric reader = new ZXing.BarcodeReaderGeneric();
            //    reader.AutoRotate = true;
            //    reader.Options.TryHarder = true;

            //    ZXing.Result[] results = null;

            //    try
            //    {
            //        Bitmap bitmap = MakeGrayscale3(frame.ToBitmap());
            //        results = reader.DecodeMultiple(bitmap);
            //        if (results != null && results.Length > 0)
            //        {
            //            ret = results[0].ToString();
            //        }
            //    }
            //    catch (ZXing.ReaderException ex)
            //    {
            //        //MessageBox.Show(resultstr, "内部错误");
            //    }
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
        }

        public int CalculateAverage(List<double> yValues)
        {
            var sortedYValues = yValues.OrderBy(y => y).ToList();
            int count = sortedYValues.Count;
            int removeCount = (int)(count * 0.2);
            var trimmedYValues = sortedYValues.Skip(removeCount).Take(count - 2 * removeCount).ToList();

            return (int)trimmedYValues.Average();
        }

        public Bitmap MakeGrayscale3(Bitmap original)
        {
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);

            //get a graphics object from the new image
            Graphics g = Graphics.FromImage(newBitmap);

            //create the grayscale ColorMatrix
            System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(
              new float[][]
              {
                 new float[] {.3f, .3f, .3f, 0, 0},
                 new float[] {.59f, .59f, .59f, 0, 0},
                 new float[] {.11f, .11f, .11f, 0, 0},
                 new float[] {0, 0, 0, 1, 0},
                 new float[] {0, 0, 0, 0, 1}
              });

            //create some image attributes
            ImageAttributes attributes = new ImageAttributes();

            //set the color matrix attribute
            attributes.SetColorMatrix(colorMatrix);

            //draw the original image on the new image
            //using the grayscale color matrix
            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
               0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);

            //dispose the Graphics object
            g.Dispose();
            original.Dispose();
            return newBitmap;
        }
    }
}