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
using Microsoft.Xaml.Behaviors.Media;
using System.Net.NetworkInformation;
using OpenCvSharp;
using System.Net.Http;

namespace WATA.LIS.Core.Services.ServiceImpl
{
    public class StatusService_DHL_KOREA : IStatusService
    {
        IEventAggregator _eventAggregator;

        // Config 데이터 클래스
        private MainConfigModel m_mainConfigModel;
        private WeightConfigModel m_weightConfig;
        private DistanceConfigModel m_distanceConfig;
        private RFIDConfigModel m_rfidConfig;
        private VisionCamConfigModel m_visionCamConfig;
        private NAVConfigModel m_navConfig;
        private LIVOXConfigModel m_livoxConfig;
        private DisplayConfigModel m_displayConfig;

        // 기초 데이터 클래스
        private BasicInfoModel m_basicInfoModel;
        private CellInfoModel m_cellInfoModel;
        private bool m_getBasicInfo = false;

        // 중량센서 데이터 클래스
        private WeightSensorModel m_weightModel;
        private List<WeightSensorModel> m_weight_list;
        private readonly int m_weight_sample_size = 8;
        private int m_event_weight = 0;
        private bool m_guideWeightStart;

        // 거리센서 데이터 클래스
        private DistanceSensorModel m_distanceModel;
        private List<DistanceSensorModel> m_distance_list;
        private readonly int m_distance_sample_size = 10;
        private int m_curr_distance = 0;
        private int m_event_distance = 0;
        private bool m_withoutLivox = false;
        private bool m_afterCallLivox = false;

        // RFID 데이터 클래스
        private Keonn2ch_Model m_rfidModel;
        private string m_curr_epc = "";
        private string m_event_epc = "";
        private int m_no_epcCnt;
        private bool m_container_ok_buzzer = false;

        // VisionCam 데이터 클래스
        private VisionCamModel m_visionModel;
        private string m_curr_QRcode = "";
        private string m_event_QRcode = "";
        private int m_no_QRcnt;
        private int m_visionPickupCnt;
        private int m_visionDropCnt;

        // LiDAR_2D 데이터 클래스
        private NAVSensorModel m_navModel;
        private string m_ActionZoneId = "";
        private string m_ActionZoneName = "";

        // LiDAR_3D 데이터 클래스
        private LIVOXModel m_livoxModel;
        private PublisherSocket _publisherSocket;
        private SubscriberSocket _subscriberSocket;
        private float m_event_width = 0;
        private float m_event_height = 0;
        private float m_event_length = 0;
        private string m_event_points = "";
        private float m_curr_height = 0;
        //private string m_dummy_points = @"[{""x"":-0.048055171966552734, ""y"":-0.8196065425872803, ""z"":-0.2845831513404846},{""x"":-0.06856584548950195,""y"":-0.6495615839958191,""z"":-0.266329288482666},{ ""x"":-0.11397743225097656,""y"":-0.6739144325256348,""z"":-0.18433888256549835},{ ""x"":-0.08682966232299805,""y"":-0.46766698360443115,""z"":-0.2857128977775574},{ ""x"":0.011022567749023438,""y"":-0.4979143738746643,""z"":-0.2583388686180115},{ ""x"":-0.09601449966430664,""y"":-0.4919881820678711,""z"":-0.18945272266864777},{ ""x"":0.059177398681640625,""y"":-0.04389047622680664,""z"":-0.3339698314666748},{ ""x"":-0.10425853729248047,""y"":0.029871582984924316,""z"":0.1271665394306183},{ ""x"":0.028022289276123047,""y"":0.03933560848236084,""z"":0.15691113471984863},{ ""x"":-0.09919500350952148,""y"":0.004209756851196289,""z"":0.3175625205039978},{ ""x"":0.07118368148803711,""y"":0.029395103454589844,""z"":0.31595587730407715},{ ""x"":-0.08274316787719727,""y"":-0.008076786994934082,""z"":0.4403194785118103},{ ""x"":0.07884597778320312,""y"":0.01820218563079834,""z"":0.44426819682121277},{ ""x"":-0.08893346786499023,""y"":0.19411492347717285,""z"":-0.2754833698272705},{ ""x"":0.03559112548828125,""y"":0.1572486162185669,""z"":-0.3402557969093323},{ ""x"":-0.08598089218139648,""y"":0.17258381843566895,""z"":-0.06646518409252167},{ ""x"":0.06773567199707031,""y"":0.14131712913513184,""z"":-0.004838868975639343},{ ""x"":-0.07169675827026367,""y"":0.04968845844268799,""z"":0.07082261145114899},{ ""x"":0.09965753555297852,""y"":0.1367586851119995,""z"":0.10288308560848236},{ ""x"":0.11103534698486328,""y"":0.1357135772705078,""z"":0.29861342906951904},{ ""x"":0.11475372314453125,""y"":0.14719581604003906,""z"":0.458082914352417},{ ""x"":-0.07363080978393555,""y"":0.28232455253601074,""z"":-0.28942376375198364},{ ""x"":0.06948375701904297,""y"":0.2944885492324829,""z"":-0.25576794147491455},{ ""x"":0.22002243995666504,""y"":0.3280855417251587,""z"":-0.19833888113498688},{ ""x"":-0.08700752258300781,""y"":0.27793216705322266,""z"":-0.10084129869937897},{ ""x"":0.09347963333129883,""y"":0.28981542587280273,""z"":-0.13262619078159332},{ ""x"":0.2533559799194336,""y"":0.32340049743652344,""z"":-0.170431450009346},{ ""x"":0.0889730453491211,""y"":0.28555166721343994,""z"":0.09841301292181015},{ ""x"":0.09506654739379883,""y"":0.2849094867706299,""z"":0.30092453956604004},{ ""x"":0.09845590591430664,""y"":0.27623558044433594,""z"":0.44636112451553345},{ ""x"":-0.48897743225097656,""y"":-0.2457605004310608,""z"":0.058913543820381165}]";

        // 인디케이터 데이터 클래스
        private IndicatorModel m_indicatorModel;
        private int m_Command = 0;
        private bool m_set_item = false;
        private bool m_set_load = false;
        private bool m_set_unload = false;
        private bool m_set_normal = false;

        // ErrorCnt 데이터 클래스
        private int m_errCnt_weight;
        private int m_errCnt_distance;
        private int m_errCnt_rfid;
        private int m_errCnt_visioncam;
        private int m_errCnt_lidar2d;
        private int m_errCnt_lidar3d;
        private int m_errCnt_indicator;
        private bool m_isError = false;
        private bool m_stop_alarm = false;
        private int m_errCnt_invalid_place;
        private int m_errCnt_invalid_place_noQR;

        // 비즈니스 로직 데이터 클래스
        private bool m_isPickUp = false;
        private bool m_isVisionPickUp = false;

        // 타이머 클래스
        DispatcherTimer m_ErrorCheck_Timer;
        DispatcherTimer m_Indicator_Timer;
        DispatcherTimer m_IsPickUp_Timer;
        DispatcherTimer m_IsDrop_Timer;
        DispatcherTimer m_MonitoringQR_Timer;
        DispatcherTimer m_MonitoringEPC_Timer;
        Stopwatch m_stopwatch;


        public StatusService_DHL_KOREA(IEventAggregator eventAggregator, IMainModel main, IRFIDModel rfidmodel,
                                    IVisionCamModel visioncCamModel, IWeightModel weightmodel, IDistanceModel distanceModel,
                                    INAVModel navModel, ILivoxModel livoxModel, IDisplayModel displayModel)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<WeightSensorEvent>().Subscribe(OnWeightSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<Keonn2chEvent>().Subscribe(OnRfidSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<HikVisionEvent>().Subscribe(OnVisionEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<NAVSensorEvent>().Subscribe(OnNAVSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<LIVOXEvent>().Subscribe(OnLivoxSensorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<IndicatorRecvEvent>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);


            m_weightConfig = (WeightConfigModel)weightmodel;
            m_distanceConfig = (DistanceConfigModel)distanceModel;
            m_rfidConfig = (RFIDConfigModel)rfidmodel;
            m_visionCamConfig = (VisionCamConfigModel)visioncCamModel;
            m_navConfig = (NAVConfigModel)navModel;
            m_livoxConfig = (LIVOXConfigModel)livoxModel;
            m_displayConfig = (DisplayConfigModel)displayModel;

            m_mainConfigModel = (MainConfigModel)main;



            DispatcherTimer AliveTimer = new DispatcherTimer();
            AliveTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            AliveTimer.Tick += new EventHandler(AliveTimerEvent);
            AliveTimer.Start();



            DispatcherTimer SendProdDataTimer = new DispatcherTimer();
            SendProdDataTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            SendProdDataTimer.Tick += new EventHandler(SendProdDataToBackEnd);
            SendProdDataTimer.Start();


            m_ErrorCheck_Timer = new DispatcherTimer();
            m_ErrorCheck_Timer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            m_ErrorCheck_Timer.Tick += new EventHandler(ErrorCheckTimerEvent);
            m_ErrorCheck_Timer.Start();



            m_MonitoringQR_Timer = new DispatcherTimer();
            m_MonitoringQR_Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_MonitoringQR_Timer.Tick += new EventHandler(MonitoringVisonTimerEvent);
            m_MonitoringQR_Timer.Start();



            m_MonitoringQR_Timer = new DispatcherTimer();
            m_MonitoringQR_Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_MonitoringQR_Timer.Tick += new EventHandler(MonitoringVisionPickupTimerEvent);
            m_MonitoringQR_Timer.Start();



            m_MonitoringEPC_Timer = new DispatcherTimer();
            m_MonitoringEPC_Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_MonitoringEPC_Timer.Tick += new EventHandler(MonitoringEPCTimerEvent);
            m_MonitoringEPC_Timer.Start();



            m_Indicator_Timer = new DispatcherTimer();
            m_Indicator_Timer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            m_Indicator_Timer.Tick += new EventHandler(IndicatorSendTimerEvent);
            m_Indicator_Timer.Start();



            m_IsPickUp_Timer = new DispatcherTimer();
            m_IsPickUp_Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_IsPickUp_Timer.Tick += new EventHandler(IsPickUpTimerEvent);
            m_IsPickUp_Timer.Start();



            m_IsDrop_Timer = new DispatcherTimer();
            m_IsDrop_Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_IsDrop_Timer.Tick += new EventHandler(IsDropTimerEvent);
            m_IsDrop_Timer.Start();


            m_weightModel = new WeightSensorModel();
            m_weight_list = new List<WeightSensorModel>();
            m_distanceModel = new DistanceSensorModel();
            m_distance_list = new List<DistanceSensorModel>();
            m_rfidModel = new Keonn2ch_Model();
            m_visionModel = new VisionCamModel();
            m_livoxModel = new LIVOXModel();
            m_indicatorModel = new IndicatorModel();

            //InitGetPickupStatus();
            IndicatorSendTimerEvent(null, null);
            _ = GetCellListFromPlatformAsync();
            _ = GetBasicInfoFromBackEndAsync();
        }

        private async Task<CellInfoModel> GetCellListFromPlatformAsync()
        {
            try
            {
                string param = "mapId=" + m_mainConfigModel.mapId + "&mappingId=" + m_mainConfigModel.mappingId + "&projectId=" + m_mainConfigModel.projectId;
                string url = "https://dev-lms-api.watalbs.com/monitoring/plane/plane-poc/plane-groups?" + param;
                Tools.Log($"REST Get Client url: {url}", ELogType.BackEndLog);

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    m_cellInfoModel = JsonConvert.DeserializeObject<CellInfoModel>(responseBody);
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

                                    string newGeom = $"POINT({x} {y})";
                                    m_cellInfoModel.data[i].targetGeofence[j].geom = newGeom;
                                }
                            }
                        }
                    }
                }
                //// CellInfoModel 좌표 하드코딩 (시연용)
                //m_cellInfoModel.data[0].targetGeofence[0].geom = "POINT(2.291 7.677)"; // PAL - C1
                //m_cellInfoModel.data[0].targetGeofence[1].geom = "POINT(2.345 6.222)"; // PAL - C2
                //m_cellInfoModel.data[0].targetGeofence[2].geom = "POINT(2.262 4.863)"; // PAL - C3
                //m_cellInfoModel.data[0].targetGeofence[3].geom = "POINT(2.291 3.463)"; // PAL - C4
                //m_cellInfoModel.data[0].targetGeofence[4].geom = "POINT(2.291 2.063)"; // PAL - C5

                //m_cellInfoModel.data[1].targetGeofence[0].geom = "POINT(-1.668 -7.935)"; // PLL - A - A1
                //m_cellInfoModel.data[1].targetGeofence[1].geom = "POINT(-0.318 -7.935)"; // PLL - A - A2
                //m_cellInfoModel.data[1].targetGeofence[2].geom = "POINT(1.011 -7.935)"; // PLL - A - A3
                //m_cellInfoModel.data[1].targetGeofence[3].geom = "POINT(2.411 -7.935)"; // PLL - A - A4
                //m_cellInfoModel.data[1].targetGeofence[4].geom = "POINT(3.811 -7.935)"; // PLL - A - A5
            }
            catch (Exception ex)
            {
                Tools.Log($"REST Get Client Response Error: {ex}", ELogType.SystemLog);
            }
            return m_cellInfoModel;
        }

        private async Task<BasicInfoModel> GetBasicInfoFromBackEndAsync()
        {
            try
            {
                string param = $"projectId={m_mainConfigModel.projectId}&mappingId={m_mainConfigModel.mappingId}&mapId={m_mainConfigModel.mapId}&vehicleId={m_mainConfigModel.vehicleId}";
                string url = $"https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/init?{param}";
                Tools.Log($"REST Get BasicInfo url: {url}", ELogType.BackEndLog);

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    m_basicInfoModel = JsonConvert.DeserializeObject<BasicInfoModel>(responseBody);
                    m_getBasicInfo = true;
                }
            }
            catch (Exception ex)
            {
                m_getBasicInfo = false;
                Tools.Log($"REST Get BasicInfo Response Error: {ex}", ELogType.BackEndLog);
            }
            return m_basicInfoModel;
        }

        private void GetBasicInfoFromBackEnd()
        {
            try
            {
                string param = $"projectId={m_mainConfigModel.projectId}&mappingId={m_mainConfigModel.mappingId}&mapId={m_mainConfigModel.mapId}&vehicleId={m_mainConfigModel.vehicleId}";
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
        /// 상태이상 경광등
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Pattlite_Buzzer_LED(ePlayBuzzerLed value)
        {
            if (value == ePlayBuzzerLed.CONTAINER_OK)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SIZE_CHECK_START)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern3;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Pattern2;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SIZE_MEASURE_OK)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.OFF;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.QR_PIKCUP)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern3;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Pattern2;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.QR_MEASURE_OK)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.NO_QR_PICKUP)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern3;
                model.LED_Color = eLEDColors.Purple;
                model.BuzzerPattern = eBuzzerPatterns.Pattern2;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.NO_QR_MEASURE_OK)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Purple;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SET_ITEM)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Cyan;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SET_ITEM_NORMAL)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Cyan;
                model.BuzzerPattern = eBuzzerPatterns.OFF;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SET_ITEM_PICKUP)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern3;
                model.LED_Color = eLEDColors.Cyan;
                model.BuzzerPattern = eBuzzerPatterns.Pattern2;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SET_ITEM_SIZE_CHECK_START)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern3;
                model.LED_Color = eLEDColors.Cyan;
                model.BuzzerPattern = eBuzzerPatterns.Pattern2;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SET_ITEM_MEASURE_OK)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Cyan;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.CLEAR_ITEM)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Cyan;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.DROP)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.OFF;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.DEVICE_ERROR)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern2;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Pattern4;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.DEVICE_ERROR_CLEAR)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.CHECK_COMPLETE)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.NO_QR_CHECK_COMPLETE)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Purple;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.SET_ITEM_CHECK_COMPLETE)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Cyan;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.INVALID_PLACE)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern6;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Pattern3;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
        }


        /// <summary>
        /// Device Error Check
        /// </summary>
        /// <param name="epcData"></param>
        private void ErrorCheckTimerEvent(object sender, EventArgs e)
        {
            if (m_weightConfig.weight_enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_weight++;
            }

            if (m_distanceConfig.distance_enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_distance++;
            }

            if (m_rfidConfig.rfid_enable == 1 && m_stop_alarm == false && m_rfidModel == null)
            {
                m_errCnt_rfid++;
            }

            if (m_visionCamConfig.vision_enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_visioncam++;
            }

            if (m_navConfig.NAV_Enable == 1 && m_stop_alarm == false && m_navModel == null)
            {
                m_errCnt_lidar2d++;
            }

            if (m_livoxConfig.LIVOX_Enable == 1 && m_stop_alarm == false && m_livoxModel == null)
            {
                m_errCnt_lidar3d++;
            }

            if (m_displayConfig.display_enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_indicator++;
            }



            if (m_weightConfig.weight_enable != 0 && m_errCnt_weight > 10 && m_errCnt_weight % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_weight);
                Tools.Log($"WeightSensor disconnected!!!", ELogType.SystemLog);
            }

            if (m_distanceConfig.distance_enable != 0 && m_errCnt_distance > 10 && m_errCnt_distance % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_distance);
                Tools.Log($"HeightSensor disconnected!!!", ELogType.SystemLog);
            }

            if (m_rfidConfig.rfid_enable != 0 && m_errCnt_rfid > 10 && m_errCnt_rfid % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_rfid);
                Tools.Log($"RFID disconnected!!!", ELogType.SystemLog);
            }

            if (m_visionCamConfig.vision_enable != 0 && m_errCnt_visioncam > 10 && m_errCnt_visioncam % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_visoncam);
                Tools.Log($"VisionCam disconnected!!!", ELogType.SystemLog);
            }

            if (m_navConfig.NAV_Enable != 0 && m_errCnt_lidar2d > 10 && m_errCnt_lidar2d % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_lidar2d);
                Tools.Log($"2D LiDAR disconnected!!!", ELogType.SystemLog);
            }

            if (m_errCnt_lidar3d > 10 && m_errCnt_lidar3d % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_lidar3d);
                Tools.Log($"3D LiDAR disconnected!!!", ELogType.SystemLog);
            }

            if (m_errCnt_indicator > 10 && m_errCnt_indicator % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_indicator);
                Tools.Log($"Indicator disconnected!!!", ELogType.SystemLog);
            }

            if (m_errCnt_weight <= 10 &&
                m_errCnt_distance <= 10 &&
                m_errCnt_rfid <= 10 &&
                m_errCnt_visioncam <= 10 &&
                m_errCnt_lidar2d <= 10 &&
                m_errCnt_lidar3d <= 10 &&
                m_errCnt_indicator <= 10 &&
                m_isError == true)
            {
                m_isError = false;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR_CLEAR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_clear);
            }
        }


        /// <summary>
        /// 중량 센서
        /// </summary>
        /// <param name="model"></param>
        private void OnWeightSensorEvent(WeightSensorModel model)
        {
            try
            {
                if (model.RightOnline == true && model.LeftOnline == true)
                {
                    m_errCnt_weight = 0;
                }
                else
                {
                    Tools.Log($"Weight Sensor Online disconnected!!!", ELogType.SystemLog);
                    return;
                }

                m_weightModel.LeftWeight = model.LeftWeight <= 0 ? 0 : model.LeftWeight;
                m_weightModel.RightWeight = model.RightWeight <= 0 ? 0 : model.RightWeight;
                m_weightModel.GrossWeight = model.GrossWeight <= 0 ? 0 : model.GrossWeight;

                if (m_weightModel.GrossWeight >= 0 && model != null)
                {
                    m_weight_list.Add(model);
                }

                if (m_weight_list.Count > m_weight_sample_size)
                {
                    if (m_weight_list.Count != 0) m_weight_list.RemoveAt(0);
                }
            }
            catch
            {
                Tools.Log($"Weight Sensor Read Error!!!", ELogType.SystemLog);
            }
        }


        /// <summary>
        /// 거리 센서
        /// </summary>
        /// <param name="model"></param>
        private void OnDistanceSensorEvent(DistanceSensorModel model)
        {
            if (model == null) return;

            m_distanceModel = model;
            m_errCnt_distance = 0;

            //DistanceSensorHardCoding(m_distanceModel.Distance_mm);

            m_curr_distance = m_distanceModel.Distance_mm - 60 - m_distanceConfig.pick_up_distance_threshold;
            if (m_curr_distance < 0)
            {
                m_curr_distance = 0;
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
                m_no_epcCnt = 0;
                m_curr_epc = epcData[0].EPC;

                m_rfidModel.CONNECTED = true;
                m_rfidModel.EPC = epcData[0].EPC;
                m_rfidModel.RSSI = epcData[0].RSSI;
                m_rfidModel.READCNT = epcData[0].READCNT;
                m_rfidModel.TS = epcData[0].TS;
            }
            else if (epcData != null && epcData.Count == 0)
            {
                m_curr_epc = "";

                m_rfidModel.CONNECTED = true;
                m_rfidModel.EPC = "";
                m_rfidModel.RSSI = 0;
                m_rfidModel.READCNT = 0;
                m_rfidModel.TS = DateTime.Now;
            }
        }

        private void MonitoringEPCTimerEvent(object sender, EventArgs e)
        {
            // PickUp 중에 컨테이너 EPC에 DA 나 DC가 포함
            if (m_curr_epc.Contains("DA") || m_curr_epc.Contains("DC"))
            {
                // 새로 인식된 EPC일 경우
                if (m_event_epc != m_curr_epc)
                {
                    m_event_epc = m_curr_epc;
                    _eventAggregator.GetEvent<HittingEPC_Event>().Publish(m_event_epc);

                    if (m_curr_epc.Contains("DC"))
                    {
                        SendBackEndContainerGateEvent();
                    }
                }

                if (m_event_epc.Contains("DC") && m_isPickUp == true && m_event_QRcode == "")
                {
                    if (m_errCnt_invalid_place_noQR % 30 == 0) Pattlite_Buzzer_LED(ePlayBuzzerLed.INVALID_PLACE);
                    m_errCnt_invalid_place_noQR++;
                }
            }

            // EPC 인식 없는 상태에서 No EPC 카운트
            if (m_rfidModel.EPC == "")
            {
                m_no_epcCnt++;
                m_curr_epc = "";
            }

            // Clear EPC Code
            if (m_no_epcCnt > 30)
            {
                m_event_epc = "";
                m_errCnt_invalid_place_noQR = 0;
                m_container_ok_buzzer = false;
                _eventAggregator.GetEvent<HittingEPC_Event>().Publish(m_event_epc);
            }
        }


        /// <summary>
        /// VisonCam 센서
        /// </summary>
        /// <param name="obj"></param>
        private void OnVisionEvent(VisionCamModel model)
        {
            if (model.connected == true) m_errCnt_visioncam = 0;

            m_visionModel = model;

            // wata 헤더 포함된 QR이 읽힐경우 할당
            if (m_visionModel.QR.Contains("wata") == m_set_item == false)
            {
                m_no_QRcnt = 0;
                m_curr_QRcode = m_visionModel.QR;
            }

            // PickupDepth 값이 Threshold 값 이하일 경우 픽업 카운트 증가. 반대일 경우 드롭 카운트 증가.
            if (m_visionModel.PIKCUP_DEPTH <= 500)
            {
                m_visionPickupCnt++;
            }
            else if (m_visionModel.PIKCUP_DEPTH > 500)
            {
                m_visionDropCnt++;
            }
        }

        private void MonitoringVisonTimerEvent(object sender, EventArgs e)
        {
            // 픽업 전 wata 헤더 포함된 QR 인식한 상태
            if (m_curr_QRcode.Contains("wata") && m_Command != 1 && m_isPickUp == false && m_event_weight < 10)
            {
                m_Command = -1;
                m_event_QRcode = m_curr_QRcode;
                _eventAggregator.GetEvent<HittingQR_Event>().Publish(m_event_QRcode);
            }

            // QR에 wata 없을 시 No QR 카운트
            if (!m_visionModel.QR.Contains("wata"))
            {
                m_no_QRcnt++;
                m_curr_QRcode = "";
            }

            // 50 프레임간 QR 인식 안될 시 QR 초기화
            if (!m_visionModel.QR.Contains("wata") && m_no_QRcnt > 150 && m_isPickUp == false)
            {
                m_Command = 0;
                m_event_QRcode = "";
                _eventAggregator.GetEvent<HittingQR_Event>().Publish(m_event_QRcode);
            }

            // 앱 물류 지정시 No QR 카운트 초기화
            if (m_set_item == true)
            {
                m_no_QRcnt = 0;
            }
        }

        private void MonitoringVisionPickupTimerEvent(object sender, EventArgs e)
        {
            // Pickup, Drop 카운트의 누적값에 의한 상태 변경
            if (m_visionPickupCnt > 10 && m_isPickUp == false)
            {
                m_isVisionPickUp = true;
                m_visionDropCnt = 0;
            }
            else if (m_visionDropCnt > 10 && m_isPickUp == true)
            {
                m_isVisionPickUp = false;
                m_visionPickupCnt = 0;
            }
        }


        /// <summary>
        /// LiDAR 2D
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

            m_navModel = navSensorModel;
        }

        private void CalcDistanceAndGetZoneID(long naviX, long naviY, bool bDrop)
        {
            List<long> calcList = new List<long>();
            long distance = 700;

            //List<string> zoneNameList = new List<string>();
            try
            {
                if (m_cellInfoModel != null && m_cellInfoModel.data.Count > 0)
                {
                    for (int i = 0; i < m_cellInfoModel.data.Count; i++)
                    {
                        if (m_ActionZoneId != "") break;

                        if (m_cellInfoModel.data[i].targetGeofence.Count > 0)
                        {
                            for (int j = m_cellInfoModel.data[i].targetGeofence.Count - 1; j >= 0; j--)
                            {

                                //string pattern = @"POINT\((\d+\.\d+) (\d+\.\d+)\)";
                                //Match match = Regex.Match(m_cellInfoModel.data[i].targetGeofence[j].geom, pattern);
                                string pattern = @"POINT\((-?\d+\.\d+) (-?\d+\.\d+)\)";
                                Match match = Regex.Match(m_cellInfoModel.data[i].targetGeofence[j].geom, pattern);
                                if (match.Success && match.Groups.Count == 3)
                                {
                                    double x = double.Parse(match.Groups[1].Value);
                                    double y = double.Parse(match.Groups[2].Value);
                                    x = Math.Truncate(x * 1000);
                                    y = Math.Truncate(y * 1000);

                                    long calcDistance = Convert.ToInt64(Math.Sqrt(Math.Pow(naviX - x, 2) + Math.Pow(naviY - y, 2)));
                                    calcList.Add(calcDistance);

                                    // Create a list to store zoneName with x, y, naviX, naviY, and calcDistance
                                    //zoneNameList.Add($"x: {x}, y: {y}, naviX: {naviX}, naviY: {naviY}, calcDistance: {calcDistance}, zoneName: {m_cellInfoModel.data[i].targetGeofence[j].zoneName}");

                                    Tools.Log($"x : " + x + " y: " + y + " calcDistance: " + calcDistance + "naviX" + naviX + "naviY" + naviY, Tools.ELogType.ActionLog);
                                    if (calcDistance < distance)
                                    {
                                        if (bDrop)
                                        {
                                            m_ActionZoneId = m_cellInfoModel.data[i].targetGeofence[j].zoneId;
                                            m_ActionZoneName = m_cellInfoModel.data[i].targetGeofence[j].zoneName;
                                        }
                                        else
                                        {
                                            m_ActionZoneId = m_cellInfoModel.data[i].targetGeofence[j].zoneId;
                                            m_ActionZoneName = m_cellInfoModel.data[i].targetGeofence[j].zoneName;
                                        }

                                        //Tools.Log($"x : " + x + " y: " + y + "zoneId: " + zoneId + " zoneName: " + zoneName + " calcDistance: " + calcDistance, Tools.ELogType.ActionLog);
                                        break;
                                    }
                                    else
                                    {
                                        m_ActionZoneId = "";
                                        m_ActionZoneName = "";
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Tools.Log($"[ERROR] Can't get cellInfoModel ", ELogType.SystemLog);
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"[ERROR] CalcDistanceAndGetZoneID : {ex.Message}", Tools.ELogType.SystemLog);
            }

        }


        /// <summary>
        /// LiDAR 3D
        /// </summary>
        /// <param name="status"></param>
        private void OnLivoxSensorEvent(LIVOXModel model)
        {
            if (model == null) return;

            m_livoxModel = model;

            m_event_distance = m_curr_distance;

            if (!m_event_epc.Contains("DA"))
            {
                m_event_width = m_livoxModel.width;
                m_event_height = m_livoxModel.height - m_event_distance;
                m_event_length = m_livoxModel.length;
                m_event_points = m_livoxModel.points;
            }
            else if (m_event_epc.Contains("DA"))
            {
                m_event_width = 0;
                m_event_height = 0;
                m_event_length = 0;
                m_event_points = "";
            }

            if (m_isPickUp == true)
            {
                CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, false);
                SendBackEndPickupAction();

                // 인디케이터 통신 핸들
                m_Command = 1;
                if (m_set_load == true) m_Command = 2;
                if (m_set_unload == true) m_Command = 3;
            }
            else if (m_isPickUp == false)
            {
                CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, false);
                SendBackEndDropAction();
            }


            // 부피 측정 완료 LED, 부저
            if (m_set_item == true && m_isError != true)
            {
                Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_MEASURE_OK);
            }
            else if (m_set_item == false && m_event_QRcode.Contains("wata") && m_isError != true)
            {
                Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_MEASURE_OK);
            }
            else if (m_set_item == false && !m_event_QRcode.Contains("wata") && m_isError != true)
            {
                Pattlite_Buzzer_LED(ePlayBuzzerLed.NO_QR_MEASURE_OK);
            }

            //if (m_event_height != m_livoxModel.length)
            //{
            //}
            IndicatorSendTimerEvent(null, null);
        }


        /// <summary>
        /// APP 인디케이터
        /// </summary>
        /// <param name="status"></param>
        private void OnIndicatorEvent(string status)
        {
            if (status.Contains("res"))
            {
                Tools.Log($"{status}", ELogType.DisplayLog);
                m_errCnt_indicator = 0;
            }

            if (status.Contains("set_item") && m_set_item == false && m_isError != true)
            {
                m_set_item = true;
                Tools.Log($"{status}", ELogType.ActionLog);

                Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_NORMAL);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.set_item);
            }

            if (status.Contains("clear_item") && m_set_item == true && m_isError != true)
            {
                m_set_item = false;
                Tools.Log($"{status}", ELogType.ActionLog);

                if (m_isPickUp == true)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.CLEAR_ITEM);
                }
                else if (m_isPickUp == false)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.CONTAINER_OK);
                    _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.clear_item);
                }
            }

            if (status.Contains("complete_item") && m_isError != true)
            {
                //_eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.register_item);
                Tools.Log($"{status}", ELogType.ActionLog);
            }

            if (status.Contains("stop_alarm"))
            {
                m_stop_alarm = true;
                m_errCnt_weight = 0;
                m_errCnt_distance = 0;
                m_errCnt_rfid = 0;
                m_errCnt_visioncam = 0;
                m_errCnt_lidar3d = 0;
                m_errCnt_indicator = 0;
            }

            if (status.Contains("normal"))
            {
                m_set_load = false;
                m_set_unload = false;
                m_set_normal = true;
                if (m_isPickUp == true) m_Command = 1;
            }

            if (status.Contains("load"))
            {
                m_set_load = true;
                m_set_unload = false;
                m_set_normal = false;
                if (m_isPickUp == true) m_Command = 2;
            }

            if (status.Contains("unload"))
            {
                m_set_load = false;
                m_set_unload = true;
                m_set_normal = false;
                if (m_isPickUp == true) m_Command = 3;
            }

            if (status.Contains("invalid_place"))
            {
                if (m_errCnt_invalid_place % 3 == 0 && m_set_load == true) Pattlite_Buzzer_LED(ePlayBuzzerLed.INVALID_PLACE);
                m_errCnt_invalid_place++;
                m_container_ok_buzzer = false;
                Tools.Log($"{status}", ELogType.DisplayLog);
            }

            if (status.Contains("correct_place"))
            {
                m_errCnt_invalid_place = 0;
                if (m_container_ok_buzzer == false)
                {
                    if (m_set_load == true) Pattlite_Buzzer_LED(ePlayBuzzerLed.CONTAINER_OK);
                    m_container_ok_buzzer = true;
                }
            }
        }

        private void IndicatorSendTimerEvent(object sender, EventArgs e)
        {
            m_indicatorModel.forklift_status.command = m_Command;
            m_indicatorModel.forklift_status.weightTotal = m_event_weight;
            m_indicatorModel.forklift_status.QR = m_event_QRcode;
            m_indicatorModel.forklift_status.visionWidth = m_event_width;
            m_indicatorModel.forklift_status.visionHeight = m_event_height;
            m_indicatorModel.forklift_status.visionDepth = m_event_length;
            m_indicatorModel.forklift_status.points = "";
            m_indicatorModel.forklift_status.epc = m_event_epc;
            m_indicatorModel.forklift_status.networkStatus = true;
            m_indicatorModel.forklift_status.weightSensorStatus = m_weightConfig.weight_enable != 0 && m_errCnt_weight > 10 ? false : true;
            m_indicatorModel.forklift_status.heightSensorStatus = m_distanceConfig.distance_enable != 0 && m_errCnt_distance > 10 ? false : true;
            m_indicatorModel.forklift_status.rfidStatus = m_rfidConfig.rfid_enable != 0 && m_errCnt_rfid > 10 ? false : true;
            m_indicatorModel.forklift_status.visionCamStatus = m_visionCamConfig.vision_enable != 0 && m_errCnt_visioncam > 10 ? false : true;
            m_indicatorModel.forklift_status.lidar2dStatus = m_navConfig.NAV_Enable != 0 && m_errCnt_lidar2d > 10 ? false : true;
            m_indicatorModel.forklift_status.lidar3dStatus = m_livoxConfig.LIVOX_Enable != 0 && m_errCnt_lidar3d > 10 ? false : true;
            m_indicatorModel.tail = true;

            string json_body = Util.ObjectToJson(m_indicatorModel);
            _eventAggregator.GetEvent<IndicatorSendEvent>().Publish(json_body);
            Tools.Log($" Send Command : {m_Command}, weight:{m_event_weight}, height:{m_event_height}", Tools.ELogType.DisplayLog);
            Tools.Log($" Send Command : {m_Command}, QR Code:{m_event_QRcode}", Tools.ELogType.DisplayLog);
            Tools.Log($" Send Command : {m_Command}", Tools.ELogType.DisplayLog);
        }


        /// <summary>
        /// 백엔드 통신
        /// </summary>
        private void AliveTimerEvent(object sender, EventArgs e)
        {
            AliveModel alive_obj = new AliveModel();
            alive_obj.alive.workLocationId = m_basicInfoModel.data[0].workLocationId;
            alive_obj.alive.vehicleId = m_basicInfoModel.data[0].vehicleId;
            alive_obj.alive.projectId = m_mainConfigModel.projectId;
            alive_obj.alive.mappingId = m_mainConfigModel.mappingId;
            alive_obj.alive.mapId = m_mainConfigModel.mapId;
            alive_obj.alive.errorCode = "0000";
            //alive_obj.alive.errorCode = SysAlarm.CurrentErr;

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

                if (m_basicInfoModel == null)
                {
                    return;
                }

                if (m_navModel == null)
                {
                    return;
                }

                ProdDataModel prodDataModel = new ProdDataModel();
                prodDataModel.mapId = m_mainConfigModel.mapId;
                prodDataModel.workLocationId = m_basicInfoModel.data[0].workLocationId;
                prodDataModel.pidx = m_basicInfoModel.data[0].pidx;
                prodDataModel.vidx = m_basicInfoModel.data[0].vidx;
                prodDataModel.vehicleId = m_basicInfoModel.data[0].vehicleId;
                prodDataModel.x = m_navModel.naviX;
                prodDataModel.y = m_navModel.naviY;
                prodDataModel.t = (int)m_navModel.naviT;
                prodDataModel.move = 1; // Stop : 0, Move : 1
                prodDataModel.load = m_isPickUp ? 1 : 0; // UnLoad : 0, Load : 1
                prodDataModel.action = m_isPickUp ? "pickup" : "drop";
                prodDataModel.result = Convert.ToInt16(m_navModel.result); // 1 : Success, other : Fail
                if (m_event_QRcode.Contains("wata")) prodDataModel.loadId = m_event_QRcode.Replace("wata", string.Empty);
                prodDataModel.epc = "DP" + m_ActionZoneName + m_event_epc;
                //prodDataModel.epc = "DP" + m_event_epc;
                //prodDataModel.errorCode = SysAlarm.CurrentErr;
                prodDataModel.errorCode = "0000";

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

        private void SendBackEndPickupAction()
        {
            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.actionInfo.workLocationId = m_basicInfoModel.data[0].workLocationId;
            ActionObj.actionInfo.vehicleId = m_mainConfigModel.vehicleId;
            ActionObj.actionInfo.projectId = m_mainConfigModel.projectId;
            ActionObj.actionInfo.mappingId = m_mainConfigModel.mappingId;
            ActionObj.actionInfo.mapId = m_mainConfigModel.mapId;
            ActionObj.actionInfo.action = "pickup";
            ActionObj.actionInfo.loadRate = m_event_weight.ToString();
            ActionObj.actionInfo.loadWeight = m_event_weight;
            if (m_event_QRcode.Contains("wata")) ActionObj.actionInfo.loadId = m_event_QRcode.Replace("wata", string.Empty);
            ActionObj.actionInfo.shelf = false;
            ActionObj.actionInfo.loadMatrixRaw = "10";
            ActionObj.actionInfo.loadMatrixColumn = "10";
            ActionObj.actionInfo.height = (m_event_distance).ToString();
            ActionObj.actionInfo.visionWidth = m_event_width;
            ActionObj.actionInfo.visionHeight = m_event_height;
            ActionObj.actionInfo.visionDepth = m_event_length;
            ActionObj.actionInfo.loadMatrix = [10, 10, 10];
            ActionObj.actionInfo.plMatrix = m_event_points;
            ActionObj.actionInfo.x = m_navModel.naviX;
            ActionObj.actionInfo.y = m_navModel.naviY;
            ActionObj.actionInfo.t = (int)m_navModel.naviT;

            if (m_event_epc == "")
            {
                if (m_ActionZoneName != "")
                {
                    ActionObj.actionInfo.epc = "DP" + m_ActionZoneName;
                    ActionObj.actionInfo.cepc = "CB202412011622";
                }
                else
                {
                    ActionObj.actionInfo.epc = "";
                    ActionObj.actionInfo.cepc = "";
                }
            }
            else if (m_event_epc.Contains("DC"))
            {
                ActionObj.actionInfo.epc = m_event_epc;
                ActionObj.actionInfo.cepc = "CB202412011622";
            }
            else if (m_event_epc.Contains("DA"))
            {
                ActionObj.actionInfo.epc = m_event_epc;
                ActionObj.actionInfo.cepc = "";
                ActionObj.actionInfo.shelf = true;
            }

            if (m_ActionZoneId == null || m_ActionZoneId.Equals(""))
            {
                //ActionObj.actionInfo.zoneId = "NA";
                ActionObj.actionInfo.zoneId = "";
            }
            else
            {
                ActionObj.actionInfo.zoneId = m_ActionZoneId;
            }
            if (m_ActionZoneName == null || m_ActionZoneId.Equals(""))
            {
                //ActionObj.actionInfo.zoneName = "NA";
                ActionObj.actionInfo.zoneName = "";
            }
            else
            {
                ActionObj.actionInfo.zoneName = m_ActionZoneName;
            }

            string json_body = Util.ObjectToJson(ActionObj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

            //_eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.register_item);
            Tools.Log($"Pickup Action {json_body}", ELogType.ActionLog);

            //zone 초기화
            m_ActionZoneId = "";
            m_ActionZoneName = "";
        }

        private void SendBackEndDropAction()
        {
            m_event_distance = m_curr_distance;

            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.actionInfo.workLocationId = m_basicInfoModel.data[0].workLocationId;
            ActionObj.actionInfo.vehicleId = m_mainConfigModel.vehicleId;
            ActionObj.actionInfo.projectId = m_mainConfigModel.projectId;
            ActionObj.actionInfo.mappingId = m_mainConfigModel.mappingId;
            ActionObj.actionInfo.mapId = m_mainConfigModel.mapId;
            ActionObj.actionInfo.action = "drop";
            ActionObj.actionInfo.loadRate = m_event_weight.ToString();
            ActionObj.actionInfo.loadWeight = m_event_weight;
            if (m_event_QRcode.Contains("wata")) ActionObj.actionInfo.loadId = m_event_QRcode.Replace("wata", string.Empty);
            ActionObj.actionInfo.shelf = false;
            ActionObj.actionInfo.loadMatrixRaw = "10";
            ActionObj.actionInfo.loadMatrixColumn = "10";
            ActionObj.actionInfo.height = (m_event_distance).ToString();
            ActionObj.actionInfo.visionWidth = m_event_width;
            ActionObj.actionInfo.visionHeight = m_event_height;
            ActionObj.actionInfo.visionDepth = m_event_length;
            ActionObj.actionInfo.loadMatrix = [10, 10, 10];
            ActionObj.actionInfo.plMatrix = m_event_points;
            ActionObj.actionInfo.x = m_navModel.naviX;
            ActionObj.actionInfo.y = m_navModel.naviY;
            ActionObj.actionInfo.t = (int)m_navModel.naviT;

            if (m_event_epc == "")
            {
                if (m_ActionZoneName != "")
                {
                    ActionObj.actionInfo.epc = "DP" + m_ActionZoneName;
                    ActionObj.actionInfo.cepc = "CB202412011622";
                }
                else
                {
                    ActionObj.actionInfo.epc = "";
                    ActionObj.actionInfo.cepc = "";
                }
            }
            else if (m_event_epc.Contains("DC"))
            {
                ActionObj.actionInfo.epc = m_event_epc;
                ActionObj.actionInfo.cepc = "CB202412011622";
            }
            else if (m_event_epc.Contains("DA"))
            {
                ActionObj.actionInfo.epc = m_event_epc;
                ActionObj.actionInfo.cepc = "";
                ActionObj.actionInfo.shelf = true;
            }



            if (m_ActionZoneId == null || m_ActionZoneId.Equals(""))
            {
                //ActionObj.actionInfo.zoneId = "NA";
                ActionObj.actionInfo.zoneId = "";
            }
            else
            {
                ActionObj.actionInfo.zoneId = m_ActionZoneId;
            }
            if (m_ActionZoneName == null || m_ActionZoneId.Equals(""))
            {
                //ActionObj.actionInfo.zoneName = "NA";
                ActionObj.actionInfo.zoneName = "";
            }
            else
            {
                ActionObj.actionInfo.zoneName = m_ActionZoneName;
            }

            string json_body = Util.ObjectToJson(ActionObj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

            Tools.Log($"Drop Action {json_body}", ELogType.ActionLog);


            // 전송 후 값 초기화
            m_weight_list = new List<WeightSensorModel>();
            m_event_weight = 0;
            m_event_epc = "";
            //m_livoxModel.height = 0;

            //zone 초기화
            m_ActionZoneId = "";
            m_ActionZoneName = "";
        }

        private void SendBackEndContainerGateEvent()
        {
            if (!m_event_epc.Contains("DC")) return;
            if (m_set_load != true) return;

            ContainerGateEventModel model = new ContainerGateEventModel();
            model.containerInfo.vehicleId = m_mainConfigModel.vehicleId;
            model.containerInfo.projectId = m_mainConfigModel.projectId;
            model.containerInfo.mappingId = m_mainConfigModel.mappingId;
            model.containerInfo.mapId = m_mainConfigModel.mapId;
            model.containerInfo.cepc = "CB202412011622";
            model.containerInfo.depc = m_event_epc;
            if (m_event_QRcode.Contains("wata")) model.containerInfo.loadId = m_event_QRcode.Replace("wata", string.Empty);

            string json_body = Util.ObjectToJson(model);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndContainer;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/container-gate-event";
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);
        }


        /// <summary>
        /// 비즈니스 로직
        /// </summary>
        /// <param name="naviX"></param>
        /// <param name="naviY"></param>
        /// <param name="bDrop"></param>
        private void InitGetPickupStatus()
        {
            if (m_visionModel.PIKCUP_DEPTH > 1000)
            {
                m_isPickUp = false;
            }
            else
            {
                m_isPickUp = true;
            }
        }

        private void IsPickUpTimerEvent(object sender, EventArgs e)
        {
            try
            {
                // 픽업 판단 조건
                if (m_isPickUp == true) return;

                if (m_isVisionPickUp == false) return;

                // 물류까지의 거리 값이 1000 이하일 때 픽업 프로세스 시작 알림음 제공
                if (m_guideWeightStart == false)
                {
                    m_stopwatch = new Stopwatch();
                    if (m_stopwatch != null) m_stopwatch.Start();

                    if (m_set_item == true && m_isError != true)
                    {
                        Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_PICKUP);
                    }
                    else if (m_set_item == false && m_event_QRcode.Contains("wata") && m_isError != true)
                    {
                        Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_PIKCUP);
                    }
                    else if (m_set_item == false && !m_event_QRcode.Contains("wata") && m_isError != true)
                    {
                        Pattlite_Buzzer_LED(ePlayBuzzerLed.NO_QR_PICKUP);
                    }

                    m_guideWeightStart = true;
                }


                // 중량값 안정화까지 픽업 판단 보류
                if (m_weight_list.Count < m_weight_sample_size) return;

                int currentWeight = m_weightModel.GrossWeight;

                int minWeight = m_weight_list.Select(w => w.GrossWeight).Min();
                if (Math.Abs(currentWeight - minWeight) > currentWeight * 0.1) return;

                int maxWeight = m_weight_list.Select(w => w.GrossWeight).Max();
                if (Math.Abs(currentWeight - maxWeight) > currentWeight * 0.1) return;

                m_event_weight = m_weightModel.GrossWeight;

                // 중량 측정 소요시간 체크
                if (m_stopwatch != null)
                {
                    m_stopwatch.Stop();
                    Tools.Log($"Pickup -> Weight Check Complete : {m_stopwatch.ElapsedMilliseconds}ms", ELogType.ActionLog);
                }


                // 부피측정 시작
                m_stopwatch = new Stopwatch();
                if (m_stopwatch != null) m_stopwatch.Start();

                // 현재 높이센서 측정값이 500 이하일 때만, 리복스 데이터 요청
                if (m_curr_distance <= 500)
                {
                    m_withoutLivox = false;
                    _eventAggregator.GetEvent<CallDataEvent>().Publish();
                }
                // 현재 높이센서 측정값이 500 초과일 때, 리복스 데이터를 나중에 요청
                else
                {
                    m_withoutLivox = true;
                    m_afterCallLivox = true;
                }

                // 앱 물류 선택 X, QR 코드 X
                if (m_set_item == false && m_event_QRcode == "")
                {
                    _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.qr_check_error);
                }
                // 앱 물류 선택 X, QR 코드 O
                else if (m_set_item == false && m_event_QRcode.Contains("wata"))
                {

                }
                // 앱 물류 선택 O, QR 코드 X
                else if (m_set_item == true && m_event_QRcode == "")
                {

                }
                // 앱 물류 선택 O, QR 코드 O
                else if (m_set_item == true && m_event_QRcode.Contains("wata"))
                {

                }

                m_isPickUp = true;
                PickUpEvent();
            }
            catch
            {
                Tools.Log($"IsPickUpTimerEvent Error", ELogType.SystemLog);
            }
        }

        private void PickUpEvent()
        {
            // 인디케이터 통신 핸들
            //m_Command = 1;
            //if (m_set_load == true) m_Command = 2;
            //if (m_set_unload == true) m_Command = 3;

            // 앱 물류 선택 X, QR 코드 X
            if (m_set_item == false && !m_event_QRcode.Contains("wata"))
            {

            }
            // 앱 물류 선택 X, QR 코드 O
            else if (m_set_item == false && m_event_QRcode.Contains("wata"))
            {

            }
            // 앱 물류 선택 O, QR 코드 X
            else if (m_set_item == true && !m_event_QRcode.Contains("wata"))
            {

            }
            // 앱 물류 선택 O, QR 코드 O
            else if (m_set_item == true && m_event_QRcode.Contains("wata"))
            {

            }

            CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, false);

            if (m_withoutLivox == true)
            {
                m_event_points = "";
                CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, false);
                SendBackEndPickupAction();

                // 모든 값 측정 완료 LED, 부저
                if (m_set_item == true && m_isError != true)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_MEASURE_OK);
                }
                else if (m_set_item == false && m_event_QRcode.Contains("wata") && m_isError != true)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_MEASURE_OK);
                }
                else if (m_set_item == false && !m_event_QRcode.Contains("wata") && m_isError != true)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.NO_QR_MEASURE_OK);
                }
            }

            // 모든 값 측정 소요시간 체크
            m_stopwatch.Stop();
            Tools.Log($"Pickup -> Size Check Complete : {m_stopwatch.ElapsedMilliseconds}ms", ELogType.ActionLog);

            //로그
            Tools.Log($"Pickup Event!!! weight:{m_event_weight}kg, width:{m_event_width}, height:{m_event_height}, depth{m_event_length}", ELogType.ActionLog);
            Tools.Log($"Pickup Event!!! QR Code:{m_event_QRcode}", ELogType.ActionLog);
        }

        private void IsDropTimerEvent(object sender, EventArgs e)
        {
            try
            {
                if (m_isPickUp == false) return;

                if (m_isVisionPickUp == true) return;

                //if (m_weight_list.Count == 0 || m_weight_list == null) return;

                Tools.Log($"Drop Event!!!", ELogType.ActionLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"IsDropTimerEvent Error : {ex.Message}", ELogType.SystemLog);
            }

            m_isPickUp = false;
            DropEvent();
        }

        private void DropEvent()
        {
            // 인디케이터 통신 핸들
            m_Command = 0;
            m_set_item = false;

            // 부저 컨트롤
            Pattlite_Buzzer_LED(ePlayBuzzerLed.DROP);

            m_guideWeightStart = false;

            CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, true);

            //// 높이가 1500이상인 곳에서 픽업을 하고 높이가 1500 이하인 경우 리복스 데이터 사후 요청
            //if (m_afterCallLivox == true && m_curr_distance <= 1500)
            //{
            //    if (m_set_item == true && m_isError != true)
            //    {
            //        Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_PICKUP);
            //    }
            //    else if (m_set_item == false && m_event_QRcode.Contains("wata") && m_isError != true)
            //    {
            //        Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_PIKCUP);
            //    }
            //    else if (m_set_item == false && !m_event_QRcode.Contains("wata") && m_isError != true)
            //    {
            //        Pattlite_Buzzer_LED(ePlayBuzzerLed.NO_QR_PICKUP);
            //    }

            //    //_eventAggregator.GetEvent<CallDataEvent>().Publish();
            //    m_event_points = "";
            //    m_afterCallLivox = false;

            //    CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, true);
            //    SendBackEndDropAction();
            //}
            //// 높이가 1500이상인 곳에서 픽업을 하고 높이가 1500 이상 랙에 드롭할 경우 리복스 데이터 요청 없음
            //else if (m_afterCallLivox == true && m_curr_distance > 1500)
            //{
            //    m_event_points = "";
            //    m_afterCallLivox = false;

            //    CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, true);
            //    SendBackEndDropAction();

            //    // 측정 완료 LED, 부저
            //    if (m_set_item == true && m_isError != true)
            //    {
            //        Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_MEASURE_OK);
            //    }
            //    else if (m_set_item == false && m_event_QRcode.Contains("wata") && m_isError != true)
            //    {
            //        Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_MEASURE_OK);
            //    }
            //    else if (m_set_item == false && !m_event_QRcode.Contains("wata") && m_isError != true)
            //    {
            //        Pattlite_Buzzer_LED(ePlayBuzzerLed.NO_QR_MEASURE_OK);
            //    }
            //}

            // 정상적인 물류 드롭인 경우
            if (m_afterCallLivox == false)
            {
                CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, false);
                SendBackEndDropAction();
            }

            m_ActionZoneId = "";
            m_ActionZoneName = "";

            // 드롭 직후 픽업이벤트 발생하는 것을 방지
            //Thread.Sleep(300);
        }
    }
}
