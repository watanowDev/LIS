using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Layout;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using NetMQ;
using System.Runtime.Intrinsics.X86;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Events.NAVSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.StatusLED;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Events.LIVOX;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.ErrorCheck;
using WATA.LIS.Core.Model.Indicator;
using WATA.LIS.Core.Model.NAV;
using WATA.LIS.Core.Model.VisionCam;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.VISION;
using WATA.LIS.Core.Model.LIVOX;
using static WATA.LIS.Core.Common.Tools;
using NetMQ.Sockets;
using System.Threading.Tasks;
using WATA.LIS.Core.Events.VisionCam;

namespace WATA.LIS.Core.Services.ServiceImpl
{
    public class StatusService_Clark : IStatusService
    {
        IEventAggregator _eventAggregator;

        // Config 모델
        private RFIDConfigModel rfidConfig;
        private VisionConfigModel visionConfig;
        private WeightConfigModel weightConfig;
        private DistanceConfigModel distanceConfig;
        private LIVOXConfigModel livoxConfig;

        // 기초 데이터 모델
        private BasicInfoModel m_basicInfoModel;
        private CellInfoModel m_cellInfoModel;
        private int m_pidx { get; set; }
        private int m_vidx { get; set; }
        private string m_mapId { get; set; }
        private string m_mappingId { get; set; }
        private string m_projectId { get; set; }
        private string m_vehicle { get; set; }
        private string m_workLocationId { get; set; }
        private bool m_getBasicInfo { get; set; }

        // 중량센서 모델
        private WeightSensorModel m_weightModel;
        private List<WeightSensorModel> m_weight_list;
        private const int m_weight_sample_size = 50;
        private int m_event_weight = 0;

        // 높이센서 모델
        private DistanceSensorModel m_distanceModel;
        private int m_event_distance = 0;

        // RFID 모델
        private Keonn2ch_Model m_rfidModel;
        private string m_event_epc = "";

        // VisionCam 모델
        private VisionCamModel m_visionModel;
        private string m_event_QRcode = "";

        // 리복스 모델
        private LIVOXModel m_livoxModel;
        PublisherSocket _publisherSocket;
        SubscriberSocket _subscriberSocket;
        private float m_event_width = 0;
        private float m_event_height = 0;
        private float m_envet_length = 0;
        private string m_event_points = "";

        // 인디케이터 모델
        private IndicatorModel m_indicatorModel;
        private int _mCommand = 0;

        // 통신상태 모델



        DispatcherTimer m_IsPickUpTimer;


        private string m_epc { get; set; }
        //private string m_qr { get; set; }
        //private int m_height_distance_mm { get; set; }

        private long m_naviX { get; set; }
        private long m_naviY { get; set; }
        private long m_naviT { get; set; }
        private int m_result { get; set; }
        private string m_zoneId { get; set; }
        private string m_zoneName { get; set; }
        private bool m_is_load { get; set; }
        private List<(long, long)> isMoving { get; set; }


        private bool m_event_value = false;
        private bool m_is_unload = false;


        public StatusService_Clark(IEventAggregator eventAggregator, IMainModel main, IRFIDModel rfidmodel, IVisionModel visionModel, IWeightModel weightmodel, IDistanceModel distanceModel, ILivoxModel livoxModel)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<Keonn2chEvent>().Subscribe(OnRfidSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<WeightSensorEvent>().Subscribe(OnWeightSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<IndicatorRecvEvent>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<HikVisionEvent>().Subscribe(OnVisionEvent, ThreadOption.BackgroundThread, true);
            //_eventAggregator.GetEvent<NAVSensorEvent>().Subscribe(OnNAVSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<LIVOXEvent>().Subscribe(OnLivoxSensorEvent, ThreadOption.BackgroundThread, true);


            rfidConfig = (RFIDConfigModel)rfidmodel;
            visionConfig = (VisionConfigModel)visionModel;
            weightConfig = (WeightConfigModel)weightmodel;
            distanceConfig = (DistanceConfigModel)distanceModel;
            livoxConfig = (LIVOXConfigModel)livoxModel;


            MainConfigModel mainobj = (MainConfigModel)main;
            m_mapId = mainobj.mapId;
            m_mappingId = mainobj.mappingId;
            m_projectId = mainobj.projectId;
            m_vehicle = mainobj.vehicleId;


            //DispatcherTimer ErrorCheckTimer = new DispatcherTimer();
            //ErrorCheckTimer.Interval = new TimeSpan(0, 0, 0, 0, 2000);
            //ErrorCheckTimer.Tick += new EventHandler(StatusErrorCheckEvent);
            //ErrorCheckTimer.Start();



            //DispatcherTimer BuzzerTimer = new DispatcherTimer();
            //BuzzerTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            //BuzzerTimer.Tick += new EventHandler(BuzzerTimerEvent);



            //DispatcherTimer AliveTimer = new DispatcherTimer();
            //AliveTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            //AliveTimer.Tick += new EventHandler(AliveTimerEvent);
            //AliveTimer.Start();



            //DispatcherTimer SendProdDataTimer = new DispatcherTimer();
            //SendProdDataTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //SendProdDataTimer.Tick += new EventHandler(SendProdDataToBackEnd);
            //SendProdDataTimer.Start();



            DispatcherTimer IndicatorTimer = new DispatcherTimer();
            IndicatorTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            IndicatorTimer.Tick += new EventHandler(IndicatorSendTimerEvent);
            IndicatorTimer.Start();



            m_IsPickUpTimer = new DispatcherTimer();
            m_IsPickUpTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_IsPickUpTimer.Tick += new EventHandler(IsPickUpTimerEvent);
            m_IsPickUpTimer.Start();



            //m_IsDropTimer = new DispatcherTimer();
            //m_IsDropTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            //m_IsDropTimer.Tick += new EventHandler(IsDropTimerEvent);
            //m_IsDropTimer.Start();


            m_rfidModel = new Keonn2ch_Model();
            m_visionModel = new VisionCamModel();
            m_weightModel = new WeightSensorModel();
            m_weight_list = new List<WeightSensorModel>();
            m_livoxModel = new LIVOXModel();
            m_indicatorModel = new IndicatorModel();
            m_visionModel = new VisionCamModel();

            GetCellListFromPlatform();
            GetBasicInfoFromBackEnd();
        }



        /// <summary>
        /// 기초 데이터 취득
        /// </summary>
        private void GetCellListFromPlatform()
        {
            try
            {
                string param = "mapId=" + m_mapId + "&mappingId=" + m_mappingId + "&projectId=" + m_projectId;
                string url = "https://dev-lms-api.watalbs.com/monitoring/plane/plane-poc/plane-groups?" + param;
                Tools.Log($"REST Get Client url: {url}", ELogType.BackEndLog);

                HttpWebRequest request = WebRequest.CreateHttp(url);
                request.Method = "GET";
                request.Timeout = 30 * 1000; // 30초
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    HttpStatusCode status = resp.StatusCode;

                    if (status == HttpStatusCode.OK)
                    {
                        Stream respStream = resp.GetResponseStream();
                        using (StreamReader sr = new StreamReader(respStream))
                        {
                            m_cellInfoModel = JsonConvert.DeserializeObject<CellInfoModel>(sr.ReadToEnd());
                            for (int i = 0; i < m_cellInfoModel.data.Count; i++)
                            {
                                if (m_cellInfoModel.data[i].targetGeofence.Count > 0)
                                {
                                    for (int j = 0; j < m_cellInfoModel.data[i].targetGeofence.Count; j++)
                                    {
                                        string pattern = @"POINT\((\d+\.\d+) (\d+\.\d+)\)";
                                        Match match = Regex.Match(m_cellInfoModel.data[i].targetGeofence[j].geom, pattern);
                                        if (match.Success && match.Groups.Count == 3)
                                        {
                                            double x = double.Parse(match.Groups[1].Value);
                                            double y = double.Parse(match.Groups[2].Value);
                                            x = Math.Truncate(x * 1000);
                                            y = Math.Truncate(y * 1000);
                                            //Tools.Log($" Cell x : " + x + " y: " + y, Tools.ELogType.BackEndLog);

                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"REST Get Client Response Error: {ex}", ELogType.BackEndLog);
            }
        }

        private void GetBasicInfoFromBackEnd()
        {
            try
            {
                string param = $"projectId={m_projectId}&mappingId={m_mappingId}&mapId={m_mapId}&vehicleId={m_vehicle}";
                string url = $"https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/init?{param}";
                Tools.Log($"REST Get BasicInfo url: {url}", ELogType.BackEndLog);

                HttpWebRequest request = WebRequest.CreateHttp(url);
                request.Method = "GET";
                request.Timeout = 30 * 1000; // 30초
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    HttpStatusCode status = resp.StatusCode;

                    if (status == HttpStatusCode.OK)
                    {
                        Stream respStream = resp.GetResponseStream();
                        using (StreamReader sr = new StreamReader(respStream))
                        {
                            m_basicInfoModel = JsonConvert.DeserializeObject<BasicInfoModel>(sr.ReadToEnd());
                            m_pidx = m_basicInfoModel.data[0].pidx;
                            m_vidx = m_basicInfoModel.data[0].vidx;
                            m_workLocationId = m_basicInfoModel.data[0].workLocationId;
                            m_vehicle = m_basicInfoModel.data[0].vehicleId;
                            m_getBasicInfo = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_getBasicInfo = false;
                Tools.Log($"REST Get BasicInfo Response Error: {ex}", ELogType.BackEndLog);
            }
        }



        /// <summary>
        /// RFID 센서
        /// </summary>
        /// <param name="epcData"></param>
        private void OnRfidSensorEvent(List<Keonn2ch_Model> epcData)
        {
            if (epcData != null && epcData.Count > 0)
            {
                foreach (var epc in epcData)
                {
                    Tools.Log($"All EPC:{epc.EPC}, RSSI:{epc.RSSI}, COUNT:{epc.READCNT}, TS:{epc.TS}", ELogType.RFIDLog);
                }
                Tools.Log($"Most EPC:{epcData[0].EPC}, RSSI:{epcData[0].RSSI}, COUNT:{epcData[0].READCNT}, TS:{epcData[0].TS}", ELogType.RFIDLog);
            }

            m_rfidModel.EPC = epcData[0].EPC;
            m_rfidModel.RSSI = epcData[0].RSSI;
            m_rfidModel.READCNT = epcData[0].READCNT;
            m_rfidModel.TS = epcData[0].TS;
        }



        /// <summary>
        /// 중량 센서
        /// </summary>
        /// <param name="obj"></param>
        private void OnWeightSensorEvent(WeightSensorModel obj)
        {
            m_weight_list.Add(obj);

            if (m_weight_list.Count >= m_weight_sample_size)
            {
                m_weightModel.LeftWeight = GetStableValue(m_weight_list.Select(w => w.LeftWeight).ToList());
                m_weightModel.RightWeight = GetStableValue(m_weight_list.Select(w => w.RightWeight).ToList());
                m_weightModel.GrossWeight = GetStableValue(m_weight_list.Select(w => w.GrossWeight).ToList());
                //Tools.Log($"Weight {m_weight}", Tools.ELogType.SystemLog);

                if (m_weightModel.GrossWeight >= 10)
                {
                    m_is_load = false;
                }
                else
                {
                    m_is_load = true;
                }

                m_weight_list.RemoveAt(0);
            }
            else
            {
                m_weightModel.LeftWeight = obj.LeftWeight <= 0 ? 0 : obj.LeftWeight;
                m_weightModel.RightWeight = obj.RightWeight <= 0 ? 0 : obj.RightWeight;
                m_weightModel.GrossWeight = obj.GrossWeight <= 0 ? 0 : obj.GrossWeight;
            }
        }

        private int GetStableValue(List<int> weight_list)
        {
            int ret = 0;
            if (weight_list.Count > 0)
            {
                int average = (int)weight_list.Average();

                int squaredDifferencesSum = weight_list.Sum(x => (x - average) * (x - average));

                double variance = squaredDifferencesSum / weight_list.Count;

                double standardDeviation = Math.Sqrt(variance);

                List<int> filteredList = weight_list.Where(x => Math.Abs(x - average) <= standardDeviation).ToList();

                ret = (int)filteredList.Average();
            }
            else
            {
                Tools.Log($"Weight Sensor Read Error!!!", ELogType.SystemLog);
            }

            if (ret < 0)
            {
                ret = 0;
            }

            return ret;
        }



        /// <summary>
        /// 높이 센서
        /// </summary>
        /// <param name="obj"></param>
        private void OnDistanceSensorEvent(DistanceSensorModel obj)
        {
            m_event_distance = obj.Distance_mm;

            //Tools.Log($"!! :  {m_lastHeight}", ELogType.SystemLog);
        }

        private int CalcHeightLoadRate(int height)
        {
            Tools.Log($"##height  : {height}", ELogType.BackEndLog);
            float A = height / (float)090.0;
            float nRet = A * (float)100.0;

            Tools.Log($"##Convert  : {height}", ELogType.BackEndLog);
            if (nRet <= 0)
            {
                nRet = 0;
            }
            if (nRet >= 97)
            {
                nRet = 97;
            }
            Tools.Log($"##Height Rate  : {nRet}", ELogType.BackEndLog);
            return (int)nRet;
        }


        /// <summary>
        /// VisonCam 센서
        /// </summary>
        /// <param name="obj"></param>

        private void OnVisionEvent(VisionCamModel model)
        {
            m_visionModel = model;
            if (m_visionModel.QR.Contains("wata"))
            {
                m_event_QRcode = m_visionModel.QR;
            }
        }


        /// <summary>
        /// NAV 센서
        /// </summary>
        /// <param name="navSensorModel"></param>
        private void OnNAVSensorEvent(NAVSensorModel navSensorModel)
        {
            if (navSensorModel.result != "1")
            {
                /*
                 * NAV데이터가 들어올 경우 각 열에 첫번째 ZoneId를 기억
                 * 
                 * if(VisionDistance > 0)
                 *  열에 X 값이 동일한 경우 Y값은 비전 데이터로 navSensorModel.naviX = navSensorModel.naviX, navSensorModel.naviY = navSensorModel.naviY + visionDistance
                 *  navSensorModel.result = 1
                 * else
                 *  
                */
            }

            m_naviX = navSensorModel.naviX;
            m_naviY = navSensorModel.naviY;
            m_naviT = navSensorModel.naviT;
            m_result = Convert.ToInt16(navSensorModel.result);

            navSensorModel.zoneId = m_zoneId;
            navSensorModel.zoneName = m_zoneName;
            navSensorModel.mapId = m_mapId;
            navSensorModel.mappingId = m_mappingId;
            navSensorModel.projectId = m_projectId;
            navSensorModel.vehicleId = m_vehicle;
        }

        private void CalcDistanceAndGetZoneID(long naviX, long naviY, bool bDrop)
        {
            long distance = 300;
            m_zoneId = "";
            m_zoneName = "";
            if (m_cellInfoModel != null && m_cellInfoModel.data.Count > 0)
            {
                for (int i = 0; i < m_cellInfoModel.data.Count; i++)
                {
                    if (m_cellInfoModel.data[i].targetGeofence.Count > 0)
                    {
                        for (int j = m_cellInfoModel.data[i].targetGeofence.Count - 1; j >= 0; j--)
                        {
                            string pattern = @"POINT\((\d+\.\d+) (\d+\.\d+)\)";
                            Match match = Regex.Match(m_cellInfoModel.data[i].targetGeofence[j].geom, pattern);
                            if (match.Success && match.Groups.Count == 3)
                            {
                                double x = double.Parse(match.Groups[1].Value);
                                double y = double.Parse(match.Groups[2].Value);
                                x = Math.Truncate(x * 1000);
                                y = Math.Truncate(y * 1000);
                                long calcDistance = Convert.ToInt64(Math.Sqrt(Math.Pow(naviX - x, 2) + Math.Pow(naviY - y, 2)));
                                //Tools.Log($"x : " + x + " y: " + y + "zoneId: " + zoneId + " zoneName: " + zoneName + " calcDistance: " + calcDistance, Tools.ELogType.BackEndLog);
                                if (calcDistance < distance)
                                {
                                    if (bDrop)
                                    {
                                        m_zoneId = m_cellInfoModel.data[i].targetGeofence[j + 1].zoneId;
                                        m_zoneName = m_cellInfoModel.data[i].targetGeofence[j + 1].zoneName;
                                    }
                                    else
                                    {
                                        m_zoneId = m_cellInfoModel.data[i].targetGeofence[j].zoneId;
                                        m_zoneName = m_cellInfoModel.data[i].targetGeofence[j].zoneName;
                                    }

                                    //Tools.Log($"x : " + x + " y: " + y + "zoneId: " + zoneId + " zoneName: " + zoneName + " calcDistance: " + calcDistance, Tools.ELogType.BackEndLog);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                Tools.Log($"[ERROR] Can't get cellInfoModel ", ELogType.BackEndLog);
            }

        }

        private int IsMovingCheck(long rNaviX, long rNaviY)
        {
            int nRetFlag = 0;

            try
            {
                isMoving.Add((rNaviX, rNaviY));

                if (isMoving.Count > 1)
                {
                    long lastX = isMoving[isMoving.Count - 1].Item1;
                    long lastY = isMoving[isMoving.Count - 1].Item2;
                    long beforeX = isMoving[isMoving.Count - 2].Item1;
                    long beforeY = isMoving[isMoving.Count - 2].Item2;
                    long diffX = Math.Abs(lastX - beforeX);
                    long diffY = Math.Abs(lastY - beforeY);
                    double totalDistance = Math.Sqrt(Math.Pow(diffX, 2) + Math.Pow(diffY, 2));

                    if (totalDistance >= 300)
                    {
                        nRetFlag = 1;
                    }
                    else
                    {
                        nRetFlag = 0;
                    }

                    isMoving.RemoveRange(0, isMoving.Count - 1);
                }
            }
            catch
            {
                Tools.Log($"Failed IsMovingCheck", ELogType.BackEndLog);
            }

            return nRetFlag;
        }



        /// <summary>
        /// LIVOX 센서
        /// </summary>
        /// <param name="status"></param>
        private void OnLivoxSensorEvent(LIVOXModel LivoxSensorModel)
        {
            m_livoxModel = LivoxSensorModel;
        }

        private void SendToLivox(int commandNum)
        {
            try
            {
                // 메시지를 퍼블리시합니다.
                string message = $"LIS>MID360,{commandNum}"; // 1은 물류 부피 데이터 요청, 0은 수신완료 응답

                // 주제와 메시지를 결합하여 퍼블리시
                _publisherSocket.SendFrame(message);

                Tools.Log($"SendToLivox : {message}", Tools.ELogType.BackEndLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"Failed SendToLivox : {ex.Message}", Tools.ELogType.BackEndLog);
            }
        }

        private bool GetSizeData()
        {
            bool ret = false;
            try
            {
                // 이벤트 모델 생성
                LIVOXModel eventModel = new LIVOXModel();

                // "VISION" 주제를 구독합니다.
                _subscriberSocket.Subscribe("MID360>LIS");

                // 메시지를 수신합니다.
                string RcvStr = _subscriberSocket.ReceiveFrameString();
                if (!"".Equals(RcvStr))
                {
                    if (!RcvStr.Contains("MID360>LIS"))
                    {
                        return ret;
                    }

                    if (RcvStr.Contains("height") && RcvStr.Contains("width") && RcvStr.Contains("length") && RcvStr.Contains("result"))
                    {
                        // JSON 문자열에서 데이터를 추출합니다.
                        var jsonString = RcvStr.Substring(RcvStr.IndexOf("{"));
                        var jsonObject = JObject.Parse(jsonString);

                        eventModel.topic = "MID360>LIS";
                        eventModel.responseCode = 0;
                        eventModel.width = (int)jsonObject["width"];
                        eventModel.height = (int)jsonObject["height"];
                        eventModel.length = (int)jsonObject["length"];
                        eventModel.result = (int)jsonObject["result"]; // bool 값을 int로 변환
                        eventModel.points = jsonObject["points"].ToString();

                        _eventAggregator.GetEvent<LIVOXEvent>().Publish(eventModel);
                        Tools.Log($"height:{eventModel.width}, width:{eventModel.height}, depth:{eventModel.length}", Tools.ELogType.BackEndLog);

                        return ret = true;
                    }
                    else
                    {
                        // 부피사이즈를 읽어오지 못했을 때 처리
                        eventModel.width = -1;
                        eventModel.height = -1;
                        eventModel.length = -1;
                        eventModel.points = "";
                    }
                }
                else
                {
                    // 타임아웃 발생 시 처리
                    eventModel.width = -1;
                    eventModel.height = -1;
                    eventModel.length = -1;
                    eventModel.points = "";

                    Tools.Log("Timeout occurred while receiving message", Tools.ELogType.BackEndLog);
                }
            }
            catch (Exception ex)
            {
                // 예외 처리
                Tools.Log($"Exception occurred: {ex.Message}", Tools.ELogType.BackEndLog);
            }

            return ret;
        }



        /// <summary>
        /// 인디케이터
        /// </summary>
        /// <param name="status"></param>
        private void OnIndicatorEvent(string status)
        {
            Tools.Log($"OnIndicatorEvent {status}", ELogType.DisplayLog);

            // 통합 모니터링
            if (status == "start_unload")
            {
                m_is_unload = true;
            }

            if (status == "stop_unload")
            {
                m_is_unload = false;
            }


            //판토스 인디케이터
            if (status == "set_load")
            {
                Tools.Log(status, ELogType.BackEndLog);
            }

            if (status == "set_unload")
            {
                Tools.Log(status, ELogType.BackEndLog);
            }

            if (status == "set_normal")
            {
                Tools.Log(status, ELogType.BackEndLog);
            }
        }

        private void IndicatorSendTimerEvent(object sender, EventArgs e)
        {
            m_indicatorModel.forklift_status.command = _mCommand;
            m_indicatorModel.forklift_status.weightTotal = m_event_weight;
            m_indicatorModel.forklift_status.QR = m_event_QRcode;
            m_indicatorModel.forklift_status.visionWidth = m_event_width;
            m_indicatorModel.forklift_status.visionHeight = m_event_height;
            m_indicatorModel.forklift_status.visionDepth = m_envet_length;
            m_indicatorModel.forklift_status.points = m_event_points;
            m_indicatorModel.forklift_status.epc = m_event_epc;
            //m_indicatorModel.forklift_status.networkStatus = true;
            //m_indicatorModel.forklift_status.weightSensorStatus = true;
            //m_indicatorModel.forklift_status.visionCamStatus = true;
            //m_indicatorModel.forklift_status.lidar2dStatus = true;
            //m_indicatorModel.forklift_status.lidar3dStatus = true;
            //m_indicatorModel.forklift_status.heightSensorStatus = true;
            //m_indicatorModel.forklift_status.rfidStatus = true;

            string json_body = Util.ObjectToJson(m_indicatorModel);
            _eventAggregator.GetEvent<IndicatorSendEvent>().Publish(json_body);
            Tools.Log($" Send Command : {_mCommand}, weight:{m_weightModel.GrossWeight}, height:{m_livoxModel.width}, width:{m_livoxModel.height}, depth:{m_livoxModel.length}", Tools.ELogType.BackEndLog);
        }



        /// <summary>
        /// 상태이상 경광등
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BuzzerTimerEvent(object sender, EventArgs e)
        {

            //BuzzerTimer.Stop();

            Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
            model.LED_Pattern = eLEDPatterns.Pattern1;
            model.LED_Color = eLEDColors.OFF;
            model.BuzzerPattern = eBuzzerPatterns.Pattern1;
            model.BuzzerCount = 1;
            _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);

        }

        private void StatusErrorCheckEvent(object sender, EventArgs e)
        {
            do
            {
                //if (GlobalValue.IS_ERROR.camera == false)
                //{
                //    Tools.Log("Camera Error", Tools.ELogType.SystemLog);
                //}


                //if (GlobalValue.IS_ERROR.backend == false)
                //{
                //    Tools.Log("BackEnd Error", Tools.ELogType.SystemLog);
                //}


                //if (GlobalValue.IS_ERROR.rfid == false)
                //{
                //    Tools.Log("rifid Error", Tools.ELogType.SystemLog);
                //}



                //if (GlobalValue.IS_ERROR.distance == false)
                //{
                //    Tools.Log("distance Error", Tools.ELogType.SystemLog);
                //}



                if (GlobalValue.IS_ERROR.camera == false ||
                   GlobalValue.IS_ERROR.backend == false ||
                   GlobalValue.IS_ERROR.rfid == false ||
                   GlobalValue.IS_ERROR.distance == false)
                {

                    _eventAggregator.GetEvent<StatusLED_Event>().Publish("red");



                    break;
                }


                _eventAggregator.GetEvent<StatusLED_Event>().Publish("green");


            }
            while (false);

        }

        private void Pattlite_Buzzer_LED(ePlayBuzzerLed value)
        {
            if (value == ePlayBuzzerLed.ACTION_FAIL)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.ACTION_START)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Blue;
                model.BuzzerPattern = eBuzzerPatterns.Pattern1;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.ACTION_FINISH)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern1;
                model.LED_Color = eLEDColors.Purple;
                model.BuzzerPattern = eBuzzerPatterns.Pattern4;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.DROP)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.EMERGENCY)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Pattern1;
                model.BuzzerCount = 0;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
                //BuzzerTimer.Start();

            }

            else if (value == ePlayBuzzerLed.EMERGENCY2)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Pattern2;
                model.BuzzerCount = 0;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
                //BuzzerTimer.Start();

            }

            else if (value == ePlayBuzzerLed.MEASRUE_OK)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern1;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Pattern1;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }


        }



        /// <summary>
        /// 백엔드 전송
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AliveTimerEvent(object sender, EventArgs e)
        {
            SendAliveEvent();
        }

        private void SendAliveEvent()
        {
            AliveModel alive_obj = new AliveModel();
            alive_obj.alive.workLocationId = "CTR_PROJECT";
            alive_obj.alive.vehicleId = m_vehicle;
            alive_obj.alive.projectId = m_projectId;
            alive_obj.alive.mappingId = m_mappingId;
            alive_obj.alive.mapId = m_mapId;
            alive_obj.alive.errorCode = SysAlarm.CurrentErr;

            string json_body = Util.ObjectToJson(alive_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            //post_obj.url = "https://smp-api.watanow.com/monitoring/geofence/addition-info/logistics/heavy-equipment/alive";

            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndCurrent;
            // _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);

            Thread.Sleep(10);

            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/alive";
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

        }

        private void SendProdDataToBackEnd(object sender, EventArgs e)
        {
            try
            {
                if (m_getBasicInfo == false)
                {
                    Tools.Log($"Failed Get BasicInfo", ELogType.BackEndLog);
                    return;
                }

                ProdDataModel prodDataModel = new ProdDataModel();
                prodDataModel.pidx = m_pidx;
                prodDataModel.vidx = m_vidx;
                prodDataModel.vehicleId = m_vehicle;
                prodDataModel.x = m_naviX;
                prodDataModel.y = m_naviY;
                prodDataModel.t = (int)m_naviT;
                prodDataModel.move = IsMovingCheck(prodDataModel.x, prodDataModel.y); // Stop : 0, Move : 1
                prodDataModel.load = m_is_load ? 0 : 1; // UnLoad : 0, Load : 1
                prodDataModel.result = m_result; // 1 : Success, other : Fail
                prodDataModel.errorCode = SysAlarm.CurrentErr;

                string json_body = Util.ObjectToJson(prodDataModel);

                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/plane/plane-poc/heavy-equipment/location";

                _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

                //Tools.Log($"{json_body}", Tools.ELogType.BackEndLog);
            }
            catch
            {
                Tools.Log($"Failed SendProdData to BackEnd", ELogType.BackEndLog);
            }
        }



        /// <summary>
        /// 픽업 이벤트
        /// </summary>
        /// <param name="naviX"></param>
        /// <param name="naviY"></param>
        /// <param name="bDrop"></param>
        private void IsPickUpTimerEvent(object sender, EventArgs e)
        {
            if (m_weightModel.GrossWeight < 10)
            {
                return;
            }

            if (m_event_QRcode == "" || m_event_QRcode == null)
            {
                return;
            }

            PickUpEvent();
        }

        private void PickUpEvent()
        {
            // 픽업 시 값 고정
            m_event_weight = m_weightModel.GrossWeight;
            m_event_distance = m_livoxModel.height;
            m_event_QRcode = m_visionModel.QR;
            m_event_width = m_livoxModel.width;
            m_event_height = m_livoxModel.height;
            m_envet_length = m_livoxModel.length;
            m_event_points = m_livoxModel.points;
            m_event_epc = m_rfidModel.EPC;

            // 부피, 형상 리복스 데이터 요청
            int getLivoxctn = 0;
            while (getLivoxctn < 100)
            {
                SendToLivox(1);
                if (GetSizeData() == true)
                {
                    SendToLivox(0);
                    break;
                }
                getLivoxctn++;
            }

            // 인디케이터 통신 핸들
            _mCommand = 1;

        }



        /// <summary>
        /// 드롭 이벤트
        /// </summary>
        /// <param name="naviX"></param>
        /// <param name="naviY"></param>
        /// <param name="bDrop"></param>
        private void IsDropTimerEvent(object sender, EventArgs e)
        {
            // 드롭 시 값 고정
            m_event_weight = 0;
            m_event_distance = 0;
            m_event_QRcode = "";
            m_event_width = 0;
            m_event_height = 0;
            m_envet_length = 0;
            m_event_points = "";
            m_event_epc = "";

            //if (m_weightModel.GrossWeight >= 10)
            //{
            //    return;
            //}

            //if (m_qr != "" || m_qr != null)
            //{
            //    return;
            //}

            DropEvent();
        }

        private void DropEvent()
        {
            CalcDistanceAndGetZoneID(m_naviX, m_naviY, true);
        }
    }
}
