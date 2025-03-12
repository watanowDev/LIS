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
    public class StatusService_WATA : IStatusService
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

        // 도크 경광등 데이터 클래스
        private bool m_guide_checkDocking = false;
        private bool m_correct_placement = false;

        // 중량센서 데이터 클래스
        private WeightSensorModel m_weightModel;
        private List<WeightSensorModel> m_weight_list;
        private readonly int m_weight_sample_size = 8;
        private int m_event_weight = 0;
        private int m_get_weightCnt = 0;
        private bool m_isWeightPickup = false;

        // 거리센서 데이터 클래스
        private DistanceSensorModel m_distanceModel;
        private List<DistanceSensorModel> m_distance_list;
        private readonly int m_distance_sample_size = 10;
        private int m_curr_distance = 0;
        private int m_event_distance = 0;
        //private bool m_withoutLivox = false;
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
        private int m_visionPickupTime;

        // LiDAR_2D 데이터 클래스
        private NAVSensorModel m_navModel;
        private List<NAVSensorModel> m_nav_list = new List<NAVSensorModel>();
        private readonly int m_nav_sample_size = 10;
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

        // 인디케이터 데이터 클래스
        private IndicatorModel m_indicatorModel;
        private int m_Command = 0;
        private bool m_set_item = false;
        private bool m_set_load = false;
        private bool m_set_unload = false;
        private bool m_set_normal = false;
        private bool m_set_measure = false;
        //private bool m_set_valid_place = false;

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
        private bool m_pickupStatus = false;
        private bool m_isVisionPickUp = false;
        private bool m_guideMeasuringStart;
        bool m_logisData = false;

        // 타이머 클래스
        DispatcherTimer m_ErrorCheck_Timer;
        DispatcherTimer m_Indicator_Timer;
        DispatcherTimer m_IsPickUp_Timer;
        DispatcherTimer m_IsDrop_Timer;
        DispatcherTimer m_MonitoringQR_Timer;
        DispatcherTimer m_MonitoringEPC_Timer;
        DispatcherTimer m_MonitoringPickup_Timer;
        DispatcherTimer m_IsValidPlace_Timer;
        Stopwatch m_stopwatchWeight = new Stopwatch();
        Stopwatch m_stopwatchPickDrop = new Stopwatch();
        const int m_timeoutPickDrop = 5000;


        public StatusService_WATA(IEventAggregator eventAggregator, IMainModel main, IRFIDModel rfidmodel,
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

            if (m_displayConfig.display_type.Contains("Platform"))
            {
                DispatcherTimer AliveTimer = new DispatcherTimer();
                AliveTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
                AliveTimer.Tick += new EventHandler(AliveTimerEvent);
                AliveTimer.Start();



                DispatcherTimer SendProdDataTimer = new DispatcherTimer();
                SendProdDataTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
                SendProdDataTimer.Tick += new EventHandler(SendProdDataToBackEnd);
                SendProdDataTimer.Start();


                _ = GetCellListFromPlatformAsync();
                _ = GetBasicInfoFromBackEndAsync();
            }
            else if (m_displayConfig.display_type.Contains("StandAlone"))
            {

            }



            m_IsValidPlace_Timer = new DispatcherTimer();
            m_IsValidPlace_Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_IsValidPlace_Timer.Tick += new EventHandler(IsCorrectDockingTimerEvent);
            m_IsValidPlace_Timer.Start();



            m_ErrorCheck_Timer = new DispatcherTimer();
            m_ErrorCheck_Timer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            m_ErrorCheck_Timer.Tick += new EventHandler(ErrorCheckTimerEvent);
            m_ErrorCheck_Timer.Start();



            m_MonitoringQR_Timer = new DispatcherTimer();
            m_MonitoringQR_Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_MonitoringQR_Timer.Tick += new EventHandler(MonitoringVisonQRTimerEvent);
            m_MonitoringQR_Timer.Start();



            m_MonitoringPickup_Timer = new DispatcherTimer();
            m_MonitoringPickup_Timer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            m_MonitoringPickup_Timer.Tick += new EventHandler(MonitoringPickupTimerEvent);
            m_MonitoringPickup_Timer.Start();



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

            m_stopwatchPickDrop.Start();
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

        private void StartMaesuringBuzzer()
        {
            // 측정 프로세스 시작 알림음 제공. 이미 제공된 경우 건너뜀.
            if (m_guideMeasuringStart == false)
            {
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

                m_guideMeasuringStart = true;
            }
        }

        private void CheckExceptionBuzzer()
        {
            // 앱 물류 선택 X, QR 코드 X
            if (m_set_item == false && !m_event_QRcode.Contains("wata"))
            {
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.qr_check_error);
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
        }

        private void FinishMeasuringBuzzer()
        {
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

        private void IsCorrectDockingTimerEvent(object sender, EventArgs e)
        {

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

        private void TryGetStableWeight()
        {
            m_stopwatchWeight.Reset();
            m_stopwatchWeight.Start();

            int currentWeight = m_weightModel.GrossWeight;
            int minWeight = m_weight_list.Select(w => w.GrossWeight).Min();
            int maxWeight = m_weight_list.Select(w => w.GrossWeight).Max();

            // 안정된 중량값 취득
            if (m_weight_list.Count < m_weight_sample_size)
            {
                m_event_weight = -1;
                m_get_weightCnt++;
                return;
            }
            else if (m_weightModel.GrossWeight < 10)
            {
                m_event_weight = -1;
                m_get_weightCnt++;
                return;
            }
            else if (Math.Abs(currentWeight - minWeight) > currentWeight * 0.1)
            {
                m_event_weight = -1;
                m_get_weightCnt++;
                return;
            }
            else if (Math.Abs(currentWeight - maxWeight) > currentWeight * 0.1)
            {
                m_event_weight = -1;
                m_get_weightCnt++;
                return;
            }

            m_event_weight = m_weightModel.GrossWeight;

            m_stopwatchWeight.Stop();

            Tools.Log($"Weight Measuring Time: {m_stopwatchWeight.ElapsedMilliseconds}ms", ELogType.ActionLog);

            IndicatorSendTimerEvent(null, null);
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

            m_curr_distance = m_distanceModel.Distance_mm - m_distanceConfig.pick_up_distance_threshold;

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
            // 상차 하차 지시 없고 하이랙 EPC 인식 시
            if (m_curr_epc.Contains("DA") && m_set_normal == true)
            {
                m_no_epcCnt = 0;
                // 새로 인식된 EPC일 경우
                if (m_event_epc != m_curr_epc)
                {
                    m_event_epc = m_curr_epc;
                    _eventAggregator.GetEvent<HittingEPC_Event>().Publish(m_event_epc);
                }
            }

            // 상차 하차 지시가 있으면서 도크 EPC 인식 시
            if (m_curr_epc.Contains("DC") && (m_set_load == true || m_set_unload == true))
            {
                m_no_epcCnt = 0;
                // 새로 인식된 EPC일 경우
                if (m_event_epc != m_curr_epc)
                {
                    m_event_epc = m_curr_epc;
                    _eventAggregator.GetEvent<HittingEPC_Event>().Publish(m_event_epc);

                    SendBackEndContainerGateEvent();
                }

                if (m_event_epc.Contains("DC") && m_pickupStatus == true && m_event_QRcode == "")
                {
                    if (m_errCnt_invalid_place_noQR % 30 == 0) Pattlite_Buzzer_LED(ePlayBuzzerLed.INVALID_PLACE);
                    m_errCnt_invalid_place_noQR++;
                }
            }

            // 상차 하차 지시 없고 랙 EPC 인식 없는 경우 No EPC 카운트
            if (!m_curr_epc.Contains("DA") && m_set_normal == true)
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


            // 깊이 값들을 리스트에 추가
            List<double> depthValues = new List<double>
                {
                    model.TM_DEPTH,
                    model.ML_DEPTH,
                    model.MM_DEPTH,
                    model.MR_DEPTH,
                    model.BL_DEPTH,
                    model.BM_DEPTH,
                    model.BR_DEPTH
                };

            // Threshold 값 미만인 값의 개수
            int count = depthValues.Count(value => value < 550);

            // 충분한 수의 ROI에서 Threshold 값 미만일 경우 픽업으로 판단
            if (count >= 6)
            {
                m_visionPickupCnt++;
            }
            // 하단부 ROI 3개 값이 Threshold 값 미만이고, 중량값이 인식되는 경우 픽업으로 판단 (낮은 물류)
            else if (m_weightModel.GrossWeight >= 10 && model.BL_DEPTH < 550 && model.BM_DEPTH < 550 && model.BR_DEPTH < 550)
            {
                m_visionPickupCnt++;
            }
            // 충분하지 못한 수의 ROI에서 Threshold 값 미만이고, 중량값이 인식되지 않는 경우 드롭으로 판단
            else if (count <= 2 && m_weightModel.GrossWeight < 10)
            {
                m_visionDropCnt++;
            }
        }

        private void MonitoringVisonQRTimerEvent(object sender, EventArgs e)
        {
            // 픽업 전 wata 헤더 포함된 QR 인식한 상태
            if (m_curr_QRcode.Contains("wata") && m_Command != 1 && m_pickupStatus == false && m_event_weight < 10)
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

            // 150 프레임간 QR 인식 안될 시 QR 초기화
            if (!m_visionModel.QR.Contains("wata") && m_no_QRcnt > 150 && m_pickupStatus == false)
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


        /// <summary>
        /// LiDAR 2D
        /// </summary>
        /// <param name="navSensorModel"></param>
        private void OnNAVSensorEvent(NAVSensorModel navSensorModel)
        {
            if (navSensorModel.result != "1")
            {
                // 1이 아닌 값은 신뢰성이 떨어지는 데이터임.
            }

            m_navModel = navSensorModel;

            if (m_navModel.result == "1" && m_navModel != null)
            {
                m_nav_list.Add(m_navModel);

                if (m_nav_list.Count > m_nav_sample_size)
                {
                    if (m_nav_list.Count != 0) m_nav_list.RemoveAt(0);
                }
            }
        }

        private void CalcDistanceAndGetZoneID(long naviX, long naviY, long naviT, bool bDrop)
        {
            List<long> calcList = new List<long>();
            long distance = 1000;

            (naviX, naviY) = AdjustCoordinates(naviX, naviY, (int)naviT, "pickdrop");

            try
            {
                if (m_cellInfoModel != null && m_cellInfoModel.data.Count > 0)
                {
                    long minDistance = long.MaxValue;
                    string closestZoneId = "";
                    string closestZoneName = "";

                    for (int i = 0; i < m_cellInfoModel.data.Count; i++)
                    {
                        if (m_cellInfoModel.data[i].targetGeofence.Count > 0)
                        {
                            for (int j = m_cellInfoModel.data[i].targetGeofence.Count - 1; j >= 0; j--)
                            {
                                string pattern = @"POINT\((-?\d+\.\d+) (-?\d+\.\d+)\)";
                                Match match = Regex.Match(m_cellInfoModel.data[i].targetGeofence[j].geom, pattern);
                                if (match.Success && match.Groups.Count == 3)
                                {
                                    double cellX = double.Parse(match.Groups[1].Value);
                                    double cellY = double.Parse(match.Groups[2].Value);
                                    cellX = Math.Truncate(cellX * 1000);
                                    cellY = Math.Truncate(cellY * 1000);

                                    long calcDistance = Convert.ToInt64(Math.Sqrt(Math.Pow(naviX - cellX, 2) + Math.Pow(naviY - cellY, 2)));
                                    Tools.Log($"ZoneName:{m_cellInfoModel.data[i].targetGeofence[j].zoneName}, X:{cellX}, Y:{cellY}, Dist:{calcDistance}, navX:{naviX}, navY:{naviY}", Tools.ELogType.ActionLog);

                                    if (calcDistance < minDistance)
                                    {
                                        minDistance = calcDistance;
                                        closestZoneId = m_cellInfoModel.data[i].targetGeofence[j].zoneId;
                                        closestZoneName = m_cellInfoModel.data[i].targetGeofence[j].zoneName;
                                    }
                                }
                            }
                        }
                    }

                    if (minDistance < long.MaxValue)
                    {
                        if (minDistance < distance)
                        {
                            m_ActionZoneId = closestZoneId;
                            m_ActionZoneName = closestZoneName;
                        }
                    }
                    else
                    {
                        m_ActionZoneId = "";
                        m_ActionZoneName = "";
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

        private (long newX, long newY) AdjustCoordinates(long x, long y, int angle, string action)
        {
            // 각도를 라디안으로 변환
            angle = (angle) % 3600;
            double radians = (angle / 10) * (Math.PI / 180.0);

            long newX = 0;
            long newY = 0;

            if (action == "pickdrop")
            {
                // 센서에서 물류 픽드롭 위치만큼 보정한 좌표 계산
                newX = x + (long)(m_navConfig.AdjustingPickdrop * Math.Cos(radians));
                newY = y + (long)(m_navConfig.AdjustingPickdrop * Math.Sin(radians));
            }
            else if (action == "positioning")
            {
                // 센서에서 지게차 중심축 위치만큼 보정한 좌표 계산
                newX = x + (long)(m_navConfig.AdjustingPosition * Math.Cos(radians));
                newY = y + (long)(m_navConfig.AdjustingPosition * Math.Sin(radians));
            }

            return (newX, newY);
        }

        //private bool CheckIsForward(long naviX, long naviY, long naviT, List<NAVSensorModel> list)
        //{
        //    const int requiredCount = 7;
        //    int count = 0;

        //    // 현재 헤딩 값에서 +- 60도 영역을 계산
        //    double lowerBound = (naviT - 600 + 3600) % 3600;
        //    double upperBound = (naviT + 600) % 3600;

        //    foreach (var item in list)
        //    {
        //        double deltaX = item.naviX - naviX;
        //        double deltaY = item.naviY - naviY;
        //        double angleToItem = Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI) * 10;
        //        angleToItem = (angleToItem + 3600) % 3600; // 0 ~ 3600 범위로 변환

        //        // 후진 중인지 확인
        //        if (lowerBound < upperBound)
        //        {
        //            if (angleToItem >= lowerBound && angleToItem <= upperBound)
        //            {
        //                count++;
        //            }
        //        }
        //        else
        //        {
        //            if (angleToItem >= lowerBound || angleToItem <= upperBound)
        //            {
        //                count++;
        //            }
        //        }

        //        if (count >= requiredCount)
        //        {
        //            //Debug.WriteLine($"{count}, Backward");
        //            return false; // 후진
        //        }
        //    }

        //    //Debug.WriteLine($"{count}, Forward");
        //    return true; // 전진
        //}

        private bool CheckIsForward(long naviX, long naviY, long naviT, List<NAVSensorModel> list)
        {
            const int requiredCount = 7;
            const double thresholdDistance = 200; // 20cm를 mm로 변환
            int count = 0;

            // 현재 헤딩 값에서 +- 60도 영역을 계산
            double lowerBound = (naviT - 600 + 3600) % 3600;
            double upperBound = (naviT + 600) % 3600;

            foreach (var item in list)
            {
                double deltaX = item.naviX - naviX;
                double deltaY = item.naviY - naviY;
                double angleToItem = Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI) * 10;
                angleToItem = (angleToItem + 3600) % 3600; // 0 ~ 3600 범위로 변환

                // 후진 중인지 확인
                if (lowerBound < upperBound)
                {
                    if (angleToItem >= lowerBound && angleToItem <= upperBound)
                    {
                        count++;
                    }
                }
                else
                {
                    if (angleToItem >= lowerBound || angleToItem <= upperBound)
                    {
                        count++;
                    }
                }

                // 가장 최근 naviX, naviY 값과의 거리 계산
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (distance < thresholdDistance)
                {
                    Debug.WriteLine($"{count}, Forward");
                    return true; // 20cm 미만일 경우 전진으로 반환
                }

                if (count >= requiredCount)
                {
                    Debug.WriteLine($"{count}, Backward");
                    return false; // 후진
                }
            }

            Debug.WriteLine($"{count}, Forward");
            return true; // 전진
        }


        /// <summary>
        /// LiDAR 3D
        /// </summary>
        /// <param name="status"></param>
        private void OnLivoxSensorEvent(LIVOXModel model)
        {
            if (model == null) return;

            m_livoxModel = model;

            //m_event_distance = m_curr_distance;

            if (!m_event_epc.Contains("DA"))
            {
                m_event_width = m_livoxModel.width;
                //m_event_height = m_livoxModel.height - (m_event_distance - m_distanceConfig.pick_up_distance_threshold);
                m_event_height = m_livoxModel.height;
                m_event_length = m_livoxModel.length;
                m_event_points = m_livoxModel.points;
            }

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

                Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_NORMAL);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.set_item);
            }

            if (status.Contains("clear_item") && m_set_item == true && m_isError != true)
            {
                m_set_item = false;

                if (m_pickupStatus == true)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.CLEAR_ITEM);
                }
                else if (m_pickupStatus == false)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.CONTAINER_OK);
                    _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.clear_item);
                }
            }

            if (status.Contains("complete_item") && m_isError != true)
            {

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
                if (m_pickupStatus == true) m_Command = 1;
            }

            if (status.Contains("load"))
            {
                m_set_load = true;
                m_set_unload = false;
                m_set_normal = false;
                if (m_pickupStatus == true) m_Command = 2;
            }

            if (status.Contains("unload"))
            {
                m_set_load = false;
                m_set_unload = true;
                m_set_normal = false;
                if (m_pickupStatus == true) m_Command = 3;
            }

            if (status.Contains("invalid_place"))
            {
                if (m_errCnt_invalid_place % 3 == 0 && (m_set_load == true || m_set_unload == true))
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.INVALID_PLACE);

                    // 도크 경광등 제어
                    m_guide_checkDocking = true;
                    m_correct_placement = false;
                    _eventAggregator.GetEvent<Patlite_Docker_LAMP_Event>().Publish(eLampSequence.Invalid_Docking);
                }

                m_errCnt_invalid_place++;
                m_container_ok_buzzer = false;
            }

            if (status.Contains("correct_place"))
            {
                m_errCnt_invalid_place = 0;
                if (m_container_ok_buzzer == false)
                {
                    if (m_set_load == true) Pattlite_Buzzer_LED(ePlayBuzzerLed.CONTAINER_OK);
                    m_container_ok_buzzer = true;

                    // 도크 경광등 제어
                    m_guide_checkDocking = true;
                    m_correct_placement = true;
                    _eventAggregator.GetEvent<Patlite_Docker_LAMP_Event>().Publish(eLampSequence.Correct_Docking);
                }
            }

            if (status.Contains("measure"))
            {
                m_set_measure = true;
            }
            else if (!status.Contains("measure"))
            {
                m_set_measure = false;
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
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndCurrent;

            Thread.Sleep(10);

            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/alive";
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);
        }

        private void SendProdDataToBackEnd(object sender, EventArgs e)
        {
            try
            {
                //if (m_curr_distance >= 1000 && m_curr_distance < 2000)
                //{
                //    m_curr_distance = m_curr_distance + 400;
                //}
                //else if (m_curr_distance >= 2000 && m_curr_distance < 4000)
                //{
                //    m_curr_distance = m_curr_distance + 800;
                //}

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

                (long adjustedX, long adjustedY) = AdjustCoordinates(m_navModel.naviX, m_navModel.naviY, (int)m_navModel.naviT, "positioning");

                ProdDataModel prodDataModel = new ProdDataModel();
                prodDataModel.mapId = m_mainConfigModel.mapId;
                prodDataModel.workLocationId = m_basicInfoModel.data[0].workLocationId;
                prodDataModel.pidx = m_basicInfoModel.data[0].pidx;
                prodDataModel.vidx = m_basicInfoModel.data[0].vidx;
                prodDataModel.vehicleId = m_basicInfoModel.data[0].vehicleId;
                prodDataModel.x = adjustedX;
                prodDataModel.y = adjustedY;
                prodDataModel.t = (int)m_navModel.naviT;
                prodDataModel.rotate = CheckIsForward(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, m_nav_list);
                prodDataModel.height = m_curr_distance;
                prodDataModel.move = 1; // Stop : 0, Move : 1
                prodDataModel.load = m_pickupStatus ? 1 : 0; // UnLoad : 0, Load : 1
                prodDataModel.action = m_pickupStatus ? "pickup" : "drop";
                prodDataModel.result = Convert.ToInt16(m_navModel.result); // 1 : Success, other : Fail
                if (m_event_QRcode.Contains("wata")) prodDataModel.loadId = m_event_QRcode.Replace("wata", string.Empty);
                prodDataModel.epc = "DP" + m_ActionZoneName + m_event_epc;
                prodDataModel.errorCode = "0000";
                //prodDataModel.errorCode = SysAlarm.CurrentErr;

                string json_body = Util.ObjectToJson(prodDataModel);

                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/plane/plane-poc/heavy-equipment/location";

                _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);
            }
            catch
            {
                Tools.Log($"Failed SendProdData to BackEnd", ELogType.BackEndLog);
            }
        }

        private void SendBackEndPickupAction()
        {
            if (m_curr_distance < 1000)
            {
                m_event_distance = m_curr_distance;
            }
            else if (m_curr_distance >= 1000 && m_curr_distance < 2000)
            {
                m_event_distance = m_curr_distance + 400;
            }
            else if (m_curr_distance >= 2000 && m_curr_distance < 4000)
            {
                m_event_distance = m_curr_distance + 800;
            }
            //m_event_distance = m_curr_distance;

            (long adjustedX, long adjustedY) = AdjustCoordinates(m_navModel.naviX, m_navModel.naviY, (int)m_navModel.naviT, "pickdrop");

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
            //ActionObj.actionInfo.shelf = false;
            if (m_event_epc.Contains("DA"))
            {
                ActionObj.actionInfo.shelf = true;
            }
            else
            {
                ActionObj.actionInfo.shelf = false;
            }
            ActionObj.actionInfo.loadMatrixRaw = "10";
            ActionObj.actionInfo.loadMatrixColumn = "10";
            ActionObj.actionInfo.height = (m_event_distance).ToString();
            ActionObj.actionInfo.visionWidth = m_event_width;
            ActionObj.actionInfo.visionHeight = m_event_height;
            ActionObj.actionInfo.visionDepth = m_event_length;
            ActionObj.actionInfo.loadMatrix = [10, 10, 10];
            ActionObj.actionInfo.plMatrix = m_event_points;
            ActionObj.actionInfo.x = adjustedX;
            ActionObj.actionInfo.y = adjustedY;
            ActionObj.actionInfo.t = (int)m_navModel.naviT;

            if (m_event_epc == "")
            {
                if (m_ActionZoneName != "")
                {
                    ActionObj.actionInfo.epc = "DP" + m_ActionZoneName;
                    ActionObj.actionInfo.cepc = "";
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
                ActionObj.actionInfo.cepc = "";
            }
            else if (m_event_epc.Contains("DA"))
            {
                ActionObj.actionInfo.epc = m_event_epc;
                ActionObj.actionInfo.cepc = "";
                ActionObj.actionInfo.shelf = true;
            }

            if (m_ActionZoneId == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneId = "";
            }
            else
            {
                ActionObj.actionInfo.zoneId = m_ActionZoneId;
            }
            if (m_ActionZoneName == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneName = "";
            }
            else
            {
                ActionObj.actionInfo.zoneName = m_ActionZoneName;
            }


            if (m_displayConfig.display_type.Contains("Platform"))
            {
                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);


                Tools.Log($"Pickup Event!!! weight:{m_event_weight}kg, width:{m_event_width}, height:{m_event_height}, depth:{m_event_length}, ForkHeight:{m_event_distance}, Zone:{m_event_epc}", ELogType.ActionLog);
                Tools.Log($"Pickup Event!!! QR Code:{m_event_QRcode}", ELogType.ActionLog);
                Tools.Log($"Pickup Action {json_body}", ELogType.ActionLog);
            }
            else
            {
                string json_body = Util.ObjectToJson(ActionObj);

                Tools.Log($"Pickup Event!!! weight:{m_event_weight}kg, width:{m_event_width}, height:{m_event_height}, depth:{m_event_length}, ForkHeight:{m_event_distance}, Zone:{m_event_epc}", ELogType.ActionLog);
                Tools.Log($"Pickup Event!!! QR Code:{m_event_QRcode}", ELogType.ActionLog);
                Tools.Log($"Fake Pickup Action {json_body}", ELogType.ActionLog);
            }

            //zone 초기화
            m_ActionZoneId = "";
            m_ActionZoneName = "";
        }

        private void SendBackEndDropAction()
        {
            if (m_curr_distance < 1000)
            {
                m_event_distance = m_curr_distance;
            }
            else if (m_curr_distance >= 1000 && m_curr_distance < 2000)
            {
                m_event_distance = m_curr_distance + 400;
            }
            else if (m_curr_distance >= 2000 && m_curr_distance < 4000)
            {
                m_event_distance = m_curr_distance + 800;
            }
            //m_event_distance = m_curr_distance;

            (long adjustedX, long adjustedY) = AdjustCoordinates(m_navModel.naviX, m_navModel.naviY, (int)m_navModel.naviT, "pickdrop");

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
            //ActionObj.actionInfo.shelf = false;
            if (m_event_epc.Contains("DA"))
            {
                ActionObj.actionInfo.shelf = true;
            }
            else
            {
                ActionObj.actionInfo.shelf = false;
            }
            ActionObj.actionInfo.loadMatrixRaw = "10";
            ActionObj.actionInfo.loadMatrixColumn = "10";
            ActionObj.actionInfo.height = (m_event_distance).ToString();
            ActionObj.actionInfo.visionWidth = m_event_width;
            ActionObj.actionInfo.visionHeight = m_event_height;
            ActionObj.actionInfo.visionDepth = m_event_length;
            ActionObj.actionInfo.loadMatrix = [10, 10, 10];
            ActionObj.actionInfo.plMatrix = m_event_points;
            ActionObj.actionInfo.x = adjustedX;
            ActionObj.actionInfo.y = adjustedY;
            ActionObj.actionInfo.t = (int)m_navModel.naviT;

            if (m_event_epc == "")
            {
                if (m_ActionZoneName != "")
                {
                    ActionObj.actionInfo.epc = "DP" + m_ActionZoneName;
                    ActionObj.actionInfo.cepc = "";
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
                ActionObj.actionInfo.cepc = "";
            }
            else if (m_event_epc.Contains("DA"))
            {
                ActionObj.actionInfo.epc = m_event_epc;
                ActionObj.actionInfo.cepc = "";
                ActionObj.actionInfo.shelf = true;
            }



            if (m_ActionZoneId == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneId = "";
            }
            else
            {
                ActionObj.actionInfo.zoneId = m_ActionZoneId;
            }
            if (m_ActionZoneName == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneName = "";
            }
            else
            {
                ActionObj.actionInfo.zoneName = m_ActionZoneName;
            }


            if (m_displayConfig.display_type.Contains("Platform"))
            {
                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

                Tools.Log($"Drop Event!!! weight:{m_event_weight}kg, width:{m_event_width}, height:{m_event_height}, depth:{m_event_length}, ForkHeight:{m_event_distance}, Zone:{m_event_epc}", ELogType.ActionLog);
                Tools.Log($"Drop Event!!! QR Code:{m_event_QRcode}", ELogType.ActionLog);
                Tools.Log($"Drop Action {json_body}", ELogType.ActionLog);
            }
            else
            {
                string json_body = Util.ObjectToJson(ActionObj);

                Tools.Log($"Fake Drop Event!!! weight:{m_event_weight}kg, width:{m_event_width}, height:{m_event_height}, depth: {m_event_length} , ForkHeight: {m_event_distance} , Zone: {m_event_epc}", ELogType.ActionLog);
                Tools.Log($"Fake Drop Event!!! QR Code:{m_event_QRcode}", ELogType.ActionLog);
                Tools.Log($"Fake Drop Action {json_body}", ELogType.ActionLog);
            }

            // 전송 후 값 초기화
            m_weight_list = new List<WeightSensorModel>();
            m_event_weight = 0;
            m_event_epc = "";
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
            model.containerInfo.cepc = "";
            model.containerInfo.depc = m_event_epc;
            if (m_event_QRcode.Contains("wata")) model.containerInfo.loadId = m_event_QRcode.Replace("wata", string.Empty);

            string json_body = Util.ObjectToJson(model);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndContainer;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/container-gate-event";
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);
        }

        private bool GetLogisticsLotInfo(string productId)
        {
            bool result = false;

            if (productId == "") return result;

            try
            {
                // "wata"가 포함되어 있으면 제거
                if (productId.Contains("wata"))
                {
                    productId = productId.Replace("wata", string.Empty);
                }

                string query = $"?projectId={m_mainConfigModel.projectId}&mappingId={m_mainConfigModel.mappingId}&mapId={m_mainConfigModel.mapId}&productId={productId}";
                string url = $"https://dev-lms-api.watalbs.com/monitoring/app/geofence/addition-info/logistics/lot/inventory-info{query}";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 3 * 1000; // 3초

                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    HttpStatusCode status = resp.StatusCode;
                    Stream respStream = resp.GetResponseStream();
                    using (StreamReader sr = new StreamReader(respStream))
                    {
                        string responseBody = sr.ReadToEnd();
                        JObject json = JObject.Parse(responseBody);

                        // "data" 값을 result에 담음
                        result = json["data"]?.Value<bool>() ?? false;
                    }
                }
                Tools.Log($"Is Logistic Data : {result}", ELogType.ActionLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"No Exist Logistic Data : {ex.Message}", ELogType.ActionLog);
            }

            return result;
        }


        /// <summary>
        /// 비즈니스 로직
        /// </summary>
        /// <param name="naviX"></param>
        /// <param name="naviY"></param>
        /// <param name="bDrop"></param>
        private void InitGetPickupStatus()
        {
            if (m_visionModel.ACTION_DEPTH > 1000)
            {
                m_pickupStatus = false;
            }
            else
            {
                m_pickupStatus = true;
            }
        }

        private void MonitoringPickupTimerEvent(object sender, EventArgs e)
        {
            try
            {
                // Vision Pickup 카운트의 누적값에 의한 상태 변경
                if (m_visionPickupCnt > 15 && m_pickupStatus == false && m_isWeightPickup == false)
                {
                    m_isVisionPickUp = true;
                    m_visionPickupCnt = 0;
                    m_visionDropCnt = 0;
                }
                // Vision Drop 카운트의 누적값에 의한 상태 변경
                else if (m_visionDropCnt > 2 && m_pickupStatus == true)
                {
                    m_isVisionPickUp = false;
                    m_visionPickupCnt = 0;
                    m_visionDropCnt = 0;
                }

                // 측정대기인 상태에서 15초동안 픽업이 완료되지 않을 경우 노말상태로 변경
                if (m_isVisionPickUp == true && m_pickupStatus == false)
                {
                    m_visionPickupTime++;
                    if (m_visionPickupTime > 300)
                    {
                        m_isVisionPickUp = false;
                        m_visionPickupTime = 0;
                        m_visionPickupCnt = 0;
                        m_visionDropCnt = 0;
                    }
                }
                // 안정된 중량값이 10을 넘을 경우 픽업 상태로 변경
                else if (m_weight_list.Count >= m_weight_sample_size && m_weightModel.GrossWeight > 10 && m_pickupStatus == false && m_isVisionPickUp == false)
                {
                    int currentWeight = m_weightModel.GrossWeight;
                    int minWeight = m_weight_list.Select(w => w.GrossWeight).Min();
                    int maxWeight = m_weight_list.Select(w => w.GrossWeight).Max();

                    if (Math.Abs(currentWeight - minWeight) > currentWeight * 0.1)
                    {
                        return;
                    }
                    else if (Math.Abs(currentWeight - maxWeight) > currentWeight * 0.1)
                    {
                        return;
                    }

                    m_isWeightPickup = true;
                }
                else if (m_weight_list.Count >= m_weight_sample_size && m_weightModel.GrossWeight <= 10 && m_pickupStatus == true)
                {
                    m_isWeightPickup = false;
                    //m_guideMeasuringStart = false;
                    //Pattlite_Buzzer_LED(ePlayBuzzerLed.DROP);
                }
            }
            catch
            {

            }
        }

        private void IsPickUpTimerEvent(object sender, EventArgs e)
        {
            try
            {
                if (m_stopwatchPickDrop.ElapsedMilliseconds < m_timeoutPickDrop) return;

                // 픽업 판단 조건
                if (m_pickupStatus == true) return;

                if (m_isVisionPickUp == false && m_isWeightPickup == false) return;

                // 물류 데이터 유무 확인
                if (m_guideMeasuringStart == false)
                {
                    m_logisData = GetLogisticsLotInfo(m_event_QRcode);
                }

                // 재측정 명령이 없더라도, 중량, 부피 값이 모두 있을 경우 측정 건너뜀.
                if (m_logisData == true && m_set_measure == false)
                {
                    m_pickupStatus = true;

                    // 측정 완료 부저
                    FinishMeasuringBuzzer();
                    CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, false);

                    m_event_weight = -1;
                    m_event_width = -1; m_event_height = -1; m_event_length = -1;
                    m_event_points = "";

                    PickUpEvent();
                }
                // 재측정 명령이 없더라도, 상차, 하차 지시 있는 경우 측정 건너뜀.
                else if ((m_set_load == true || m_set_load == true) && m_set_measure == false)
                {
                    m_pickupStatus = true;

                    // 측정 완료 부저
                    FinishMeasuringBuzzer();
                    CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, false);

                    m_event_weight = -1;
                    m_event_width = -1; m_event_height = -1; m_event_length = -1;
                    m_event_points = "";

                    PickUpEvent();
                }
                // 재측정 명령 있거나, 물류 정보가 없을 경우 측정 시작
                else
                {
                    if (m_guideMeasuringStart == false)
                    {
                        // 측정 시작 부저. 
                        StartMaesuringBuzzer();
                        CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, false);

                        // 타임아웃 안에 안정된 부피값 취득. 실패 시 다음 프로세스 진행.
                        if (m_event_epc.Contains("DA"))
                        {
                            m_event_width = -1; m_event_height = -1; m_event_length = -1;
                            m_event_points = "";
                        }
                        else
                        {
                            _eventAggregator.GetEvent<CallDataEvent>().Publish();
                        }
                    }

                    // 타임아웃 안에 안정된 중량값 취득. 실패 시 다음 프로세스 진행.
                    TryGetStableWeight();

                    if (m_get_weightCnt > 100)
                    {
                        m_isWeightPickup = true;
                        m_pickupStatus = true;

                        // QR 미인식 등 예외 상황 시 부저 울림
                        CheckExceptionBuzzer();

                        // 측정 완료 부저
                        FinishMeasuringBuzzer();
                        CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, false);

                        PickUpEvent();
                    }
                    // 중량값이 -1이 아닌 값이 읽혔을 경우
                    else if (m_event_weight != -1 && m_pickupStatus == false)
                    {
                        m_isWeightPickup = true;
                        m_pickupStatus = true;

                        // QR 미인식 등 예외 상황 시 부저 울림
                        CheckExceptionBuzzer();

                        // 측정 완료 부저
                        FinishMeasuringBuzzer();
                        CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, false);

                        PickUpEvent();
                    }
                }
            }
            catch
            {
                Tools.Log($"IsPickUpTimerEvent Error", ELogType.SystemLog);
            }
        }

        private void PickUpEvent()
        {
            // 인디케이터 통신 핸들
            m_Command = 1;
            if (m_set_load == true) m_Command = 2;
            if (m_set_unload == true) m_Command = 3;

            //CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, false);
            SendBackEndPickupAction();

            m_stopwatchPickDrop.Reset();
            m_stopwatchPickDrop.Start();
        }

        private void IsDropTimerEvent(object sender, EventArgs e)
        {
            try
            {
                if (m_stopwatchPickDrop.ElapsedMilliseconds < m_timeoutPickDrop) return;

                if (m_pickupStatus == false) return;

                if (m_isVisionPickUp == true) return;

                if (m_isWeightPickup == true) return;

                //if (m_weight_list.Count == 0 || m_weight_list == null) return;

                //if (m_weightModel.GrossWeight > 10) return;

                Tools.Log($"Drop Event!!!", ELogType.ActionLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"IsDropTimerEvent Error : {ex.Message}", ELogType.SystemLog);
            }

            m_pickupStatus = false;
            DropEvent();
        }

        private void DropEvent()
        {
            // 인디케이터 통신 핸들
            m_Command = 0;
            m_set_item = false;

            // 부저 컨트롤
            Pattlite_Buzzer_LED(ePlayBuzzerLed.DROP);

            // 도크 경광등 제어
            if (m_guide_checkDocking == true && m_correct_placement == true)
            {
                m_guide_checkDocking = false;
                _eventAggregator.GetEvent<Patlite_Docker_LAMP_Event>().Publish(eLampSequence.Correct_Placement);
            }
            else if (m_guide_checkDocking == true && m_correct_placement == false)
            {
                m_guide_checkDocking = false;
                _eventAggregator.GetEvent<Patlite_Docker_LAMP_Event>().Publish(eLampSequence.Invalid_Placement);
            }

            // 백엔드 통신
            CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, m_navModel.naviT, true);
            SendBackEndDropAction();

            m_stopwatchPickDrop.Reset();
            m_stopwatchPickDrop.Start();

            // 물류 데이터 초기화
            m_ActionZoneId = "";
            m_ActionZoneName = "";
            m_event_weight = 0;
            m_logisData = false;
            m_get_weightCnt = 0;

            m_guideMeasuringStart = false;
        }
    }
}
