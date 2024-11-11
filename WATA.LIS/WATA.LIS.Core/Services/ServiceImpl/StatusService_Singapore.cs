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

namespace WATA.LIS.Core.Services.ServiceImpl
{
    public class StatusService_Singapore : IStatusService
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
        public List<WeightSensorModel> m_weight_list;
        private readonly int m_weight_sample_size = 8;
        private int m_event_weight = 0;
        private bool m_guideWeightStart;

        // 높이센서 데이터 클래스
        private DistanceSensorModel m_distanceModel;
        private int m_event_distance = 0;

        // RFID 데이터 클래스
        private Keonn2ch_Model m_rfidModel;
        private string m_curr_epc = "";
        private string m_event_epc = "";
        private int m_no_epcCnt;

        // VisionCam 데이터 클래스
        private VisionCamModel m_visionModel;
        private string m_curr_QRcode = "";
        private string m_event_QRcode = "";
        private int m_no_QRcnt;

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

        // 비즈니스 로직 데이터 클래스
        private bool m_isPickUp = false;

        // 타이머 클래스
        DispatcherTimer m_ErrorCheck_Timer;
        DispatcherTimer m_Indicator_Timer;
        DispatcherTimer m_IsPickUp_Timer;
        DispatcherTimer m_IsDrop_Timer;
        DispatcherTimer m_MonitoringQR_Timer;
        DispatcherTimer m_MonitoringEPC_Timer;
        Stopwatch m_stopwatch;


        public StatusService_Singapore(IEventAggregator eventAggregator, IMainModel main, IRFIDModel rfidmodel,
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
            //MainConfigModel mainobj = (MainConfigModel)main;
            //m_mapId = mainobj.mapId;
            //m_mappingId = mainobj.mappingId;
            //m_projectId = mainobj.projectId;
            //m_vehicle = mainobj.vehicleId;



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
            m_rfidModel = new Keonn2ch_Model();
            m_visionModel = new VisionCamModel();
            m_livoxModel = new LIVOXModel();
            m_indicatorModel = new IndicatorModel();

            IndicatorSendTimerEvent(null, null);
            InitGetPickupStatus();
            GetCellListFromPlatform();
            GetBasicInfoFromBackEnd();
            //InitLivox();
        }


        /// <summary>
        /// 기초 데이터 취득
        /// </summary>
        private void GetCellListFromPlatform()
        {
            try
            {
                string param = "mapId=" + m_mainConfigModel.mapId + "&mappingId=" + m_mainConfigModel.mappingId + "&projectId=" + m_mainConfigModel.projectId;
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
                            //m_pidx = m_basicInfoModel.data[0].pidx;
                            //m_vidx = m_basicInfoModel.data[0].vidx;
                            //m_workLocationId = m_basicInfoModel.data[0].workLocationId;
                            //m_vehicle = m_basicInfoModel.data[0].vehicleId;
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
            if (value == ePlayBuzzerLed.NORMAL)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.OFF;
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

            if (m_rfidConfig.rfid_enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_rfid++;
            }

            if (m_visionCamConfig.vision_enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_visioncam++;
            }

            if (m_navConfig.NAV_Enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_lidar2d++;
            }

            if (m_livoxConfig.LIVOX_Enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_lidar3d++;
            }

            if (m_displayConfig.display_enable == 1 && m_stop_alarm == false)
            {
                m_errCnt_indicator++;
            }



            if (m_weightConfig.weight_enable != 0 && m_errCnt_weight > 5 && m_errCnt_weight % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_weight);
                Tools.Log($"WeightSensor disconnected!!!", ELogType.SystemLog);
            }

            if (m_distanceConfig.distance_enable != 0 && m_errCnt_distance > 5 && m_errCnt_distance % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_distance);
                Tools.Log($"HeightSensor disconnected!!!", ELogType.SystemLog);
            }

            if (m_rfidConfig.rfid_enable != 0 && m_errCnt_rfid > 5 && m_errCnt_rfid % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_rfid);
                Tools.Log($"RFID disconnected!!!", ELogType.SystemLog);
            }

            if (m_visionCamConfig.vision_enable != 0 && m_errCnt_visioncam > 5 && m_errCnt_visioncam % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_visoncam);
                Tools.Log($"VisionCam disconnected!!!", ELogType.SystemLog);
            }

            if (m_navConfig.NAV_Enable != 0 && m_errCnt_lidar2d > 5 && m_errCnt_lidar2d % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_lidar2d);
                Tools.Log($"2D LiDAR disconnected!!!", ELogType.SystemLog);
            }

            if (m_errCnt_lidar3d > 5 && m_errCnt_lidar3d % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_lidar3d);
                Tools.Log($"3D LiDAR disconnected!!!", ELogType.SystemLog);
            }

            if (m_errCnt_indicator > 5 && m_errCnt_indicator % 10 == 0)
            {
                m_isError = true;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DEVICE_ERROR);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.device_error_indicator);
                Tools.Log($"Indicator disconnected!!!", ELogType.SystemLog);
            }

            if (m_errCnt_weight <= 5 &&
                m_errCnt_distance <= 5 &&
                m_errCnt_rfid <= 5 &&
                m_errCnt_visioncam <= 5 &&
                m_errCnt_lidar2d <= 5 &&
                m_errCnt_lidar3d <= 5 &&
                m_errCnt_indicator <= 5 &&
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

            m_errCnt_distance = 0;
            m_event_distance = model.Distance_mm;
        }


        /// <summary>
        /// RFID 센서
        /// </summary>
        /// <param name="epcData"></param>
        private void OnRfidSensorEvent(List<Keonn2ch_Model> epcData)
        {
            if (epcData != null && epcData.Count > 0 && m_isPickUp == true)
            {
                m_no_epcCnt = 0;
                m_curr_epc = epcData[0].EPC;

                m_rfidModel.CONNECTED = true;
                m_rfidModel.EPC = epcData[0].EPC;
                m_rfidModel.RSSI = epcData[0].RSSI;
                m_rfidModel.READCNT = epcData[0].READCNT;
                m_rfidModel.TS = epcData[0].TS;
            }
            else if (epcData != null && epcData.Count == 0 && m_isPickUp == false)
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
            // PickUp 중에 EPC 인식한 상태
            if (m_curr_epc.Contains("DC") && m_isPickUp == true)
            {
                // 새로 인식된 EPC일 경우
                if (m_event_epc != m_curr_epc)
                {
                    m_event_epc = m_curr_epc;
                    _eventAggregator.GetEvent<HittingEPC_Event>().Publish(m_event_epc);
                    SendBackEndContainerGateEvent();
                }
            }

            // EPC 인식 없을 시 No EPC 카운트
            if (m_rfidModel.EPC == "")
            {
                m_no_epcCnt++;
                m_curr_epc = "";
            }

            // Clear EPC Code
            if (m_no_epcCnt > 30)
            {
                m_event_epc = "";
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

            // QR에 wata 포함 시 QR 할당
            if (m_visionModel.QR.Contains("wata") == m_set_item == false)
            {
                m_no_QRcnt = 0;
                m_curr_QRcode = m_visionModel.QR;
            }
        }

        private void MonitoringVisonTimerEvent(object sender, EventArgs e)
        {
            // 픽업 전 QR 인식한 상태
            if (m_curr_QRcode.Contains("wata") && m_Command != 1 && m_isPickUp == false && m_event_weight < 30)
            {
                // 새로 인식된 QR일 경우
                if (m_event_QRcode == "")
                {
                    m_Command = -1;
                    m_event_QRcode = m_curr_QRcode;
                    _eventAggregator.GetEvent<HittingQR_Event>().Publish(m_event_QRcode);
                }
            }

            // QR에 wata 없을 시 No QR 카운트
            if (!m_visionModel.QR.Contains("wata"))
            {
                m_no_QRcnt++;
                m_curr_QRcode = "";
            }

            // 10 프레임간 QR 인식 안될 시 QR 초기화
            if (!m_visionModel.QR.Contains("wata") && m_no_QRcnt > 10 && m_isPickUp == false)
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

            //m_naviX = navSensorModel.naviX;
            //m_naviY = navSensorModel.naviY;
            //m_naviT = navSensorModel.naviT;
            //m_result = Convert.ToInt16(navSensorModel.result);

            //navSensorModel.zoneId = m_zoneId;
            //navSensorModel.zoneName = m_zoneName;
            //navSensorModel.mapId = m_mapId;
            //navSensorModel.mappingId = m_mappingId;
            //navSensorModel.projectId = m_projectId;
            //navSensorModel.vehicleId = m_vehicle;
        }

        private void CalcDistanceAndGetZoneID(long naviX, long naviY, bool bDrop)
        {
            long distance = 300;
            //m_zoneId = "";
            //m_zoneName = "";
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
                                        m_ActionZoneId = m_cellInfoModel.data[i].targetGeofence[j + 1].zoneId;
                                        m_ActionZoneName = m_cellInfoModel.data[i].targetGeofence[j + 1].zoneName;
                                    }
                                    else
                                    {
                                        m_ActionZoneId = m_cellInfoModel.data[i].targetGeofence[j].zoneId;
                                        m_ActionZoneName = m_cellInfoModel.data[i].targetGeofence[j].zoneName;
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


        /// <summary>
        /// LiDAR 3D
        /// </summary>
        /// <param name="status"></param>
        private void OnLivoxSensorEvent(LIVOXModel model)
        {
            if (model == null) return;

            m_livoxModel = model;

            m_event_width = m_livoxModel.width;
            m_event_height = m_livoxModel.height - m_event_distance;
            m_event_length = m_livoxModel.length;
            m_event_points = m_livoxModel.points;
        }

        private void InitLivox()
        {
            try
            {
                _publisherSocket = new PublisherSocket();
                // 퍼블리셔 소켓을 5555 포트에 바인딩합니다.
                _publisherSocket.Bind("tcp://127.0.0.1:5002");

                Tools.Log($"InitLivox", Tools.ELogType.SystemLog);

                _subscriberSocket = new SubscriberSocket();
                // 서브스크라이버 소켓을 5555 포트에 연결합니다.
                _subscriberSocket.Connect("tcp://127.0.0.1:5001");

                // 타임아웃 설정
                _subscriberSocket.Options.HeartbeatTimeout = TimeSpan.FromSeconds(3);

                // _subscriberSocket가 정상적으로 열렸을 때
                m_errCnt_lidar3d = 0;
            }
            catch (Exception ex)
            {
                m_errCnt_lidar3d = 5;
                Tools.Log($"Failed InitLivox : {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        private void SendToLivox(int commandNum)
        {
            try
            {
                // 메시지를 퍼블리시합니다.
                string message = $"LIS>MID360,{commandNum}"; // 1은 물류 부피 데이터 요청, 0은 수신완료 응답

                // 주제와 메시지를 결합하여 퍼블리시
                _publisherSocket.SendFrame(message);

                m_errCnt_lidar3d = 0;
                Tools.Log($"SendToLivox : {message}", Tools.ELogType.ActionLog);
            }
            catch (Exception ex)
            {
                m_errCnt_lidar3d++;
                Tools.Log($"Failed SendToLivox : {ex.Message}", Tools.ELogType.ActionLog);
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
                        //Tools.Log($"width:{eventModel.width}, height:{eventModel.height}, depth:{eventModel.length}", Tools.ELogType.SystemLog);

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

                    Tools.Log("Timeout occurred while receiving message", Tools.ELogType.SystemLog);
                }
            }
            catch (Exception ex)
            {
                // 예외 처리
                Tools.Log($"Exception occurred: {ex.Message}", Tools.ELogType.SystemLog);
            }

            return ret;
        }


        /// <summary>
        /// APP 인디케이터
        /// </summary>
        /// <param name="status"></param>
        private void OnIndicatorEvent(string status)
        {
            if (status == "res")
            {
                Tools.Log($"{status}", ELogType.DisplayLog);
                m_errCnt_indicator = 0;
            }

            if (status == "set_item" && m_set_item == false && m_isError != true)
            {
                m_set_item = true;
                Tools.Log($"{status}", ELogType.ActionLog);

                Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_NORMAL);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.set_item);
            }

            if (status == "clear_item" && m_set_item == true && m_isError != true)
            {
                m_set_item = false;
                Tools.Log($"{status}", ELogType.ActionLog);

                if (m_isPickUp == true)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.CLEAR_ITEM);
                }
                else if (m_isPickUp == false)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.NORMAL);
                    _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.clear_item);
                }
            }

            if (status == "complete_item" && m_isError != true)
            {
                Tools.Log($"{status}", ELogType.ActionLog);

                //Pattlite_Buzzer_LED(ePlayBuzzerLed.DROP);
                _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.register_item);
            }

            if (status == "stop_alarm")
            {
                m_stop_alarm = true;
                m_errCnt_weight = 0;
                m_errCnt_distance = 0;
                m_errCnt_rfid = 0;
                m_errCnt_visioncam = 0;
                m_errCnt_lidar3d = 0;
                m_errCnt_indicator = 0;
            }

            if (status == "set_load")
            {
                m_set_load = true;
                m_set_unload = false;
                m_set_normal = false;
            }

            if (status == "set_unload")
            {
                m_set_load = false;
                m_set_unload = true;
                m_set_normal = false;
            }

            if (status == "set_normal")
            {
                m_set_load = false;
                m_set_unload = false;
                m_set_normal = true;
            }
        }

        private void IndicatorSendTimerEvent(object sender, EventArgs e)
        {
            m_indicatorModel.forklift_status.command = m_Command;
            m_indicatorModel.forklift_status.weightTotal = m_event_weight;
            m_indicatorModel.forklift_status.QR = m_event_QRcode;
            m_indicatorModel.forklift_status.visionWidth = m_event_width;
            m_indicatorModel.forklift_status.visionHeight = m_event_height - m_event_distance;
            m_indicatorModel.forklift_status.visionDepth = m_event_length;
            m_indicatorModel.forklift_status.points = m_event_points;
            m_indicatorModel.forklift_status.epc = "";
            m_indicatorModel.forklift_status.networkStatus = true;
            m_indicatorModel.forklift_status.weightSensorStatus = m_weightConfig.weight_enable != 0 && m_errCnt_weight > 5 ? false : true;
            m_indicatorModel.forklift_status.heightSensorStatus = m_distanceConfig.distance_enable != 0 && m_errCnt_distance > 5 ? false : true;
            m_indicatorModel.forklift_status.rfidStatus = m_rfidConfig.rfid_enable != 0 && m_errCnt_rfid > 5 ? false : true;
            m_indicatorModel.forklift_status.visionCamStatus = m_visionCamConfig.vision_enable != 0 && m_errCnt_visioncam > 5 ? false : true;
            m_indicatorModel.forklift_status.lidar2dStatus = m_navConfig.NAV_Enable != 0 && m_errCnt_lidar2d > 5 ? false : true;
            m_indicatorModel.forklift_status.lidar3dStatus = m_livoxConfig.LIVOX_Enable != 0 && m_errCnt_lidar3d > 5 ? false : true;
            m_indicatorModel.tail = true;
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

                if (m_basicInfoModel == null)
                {
                    return;
                }

                if (m_navModel == null)
                {
                    return;
                }

                ProdDataModel prodDataModel = new ProdDataModel();
                prodDataModel.pidx = m_basicInfoModel.data[0].pidx;
                prodDataModel.vidx = m_basicInfoModel.data[0].vidx;
                prodDataModel.vehicleId = m_basicInfoModel.data[0].vehicleId;
                prodDataModel.x = m_navModel.naviX;
                prodDataModel.y = m_navModel.naviY;
                prodDataModel.t = (int)m_navModel.naviT;
                prodDataModel.move = 1; // Stop : 0, Move : 1
                prodDataModel.load = m_isPickUp ? 0 : 1; // UnLoad : 0, Load : 1
                prodDataModel.result = Convert.ToInt16(m_navModel.result); // 1 : Success, other : Fail
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
            ActionObj.actionInfo.height = (m_event_height - m_event_distance).ToString();
            ActionObj.actionInfo.epc = m_event_epc;
            ActionObj.actionInfo.cepc = m_event_epc;
            ActionObj.actionInfo.loadId = m_event_QRcode;
            ActionObj.actionInfo.shelf = false;
            ActionObj.actionInfo.loadMatrixRaw = "10";
            ActionObj.actionInfo.loadMatrixColumn = "10";
            ActionObj.actionInfo.visionWidth = m_event_width;
            ActionObj.actionInfo.visionHeight = m_event_height - m_event_distance;
            ActionObj.actionInfo.visionDepth = m_event_length;
            ActionObj.actionInfo.loadMatrix = [10, 10, 10];
            ActionObj.actionInfo.plMatrix = m_event_points;

            if (m_ActionZoneId == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneId = "NA";
            }
            else
            {
                ActionObj.actionInfo.zoneId = m_ActionZoneId;
            }


            if (m_ActionZoneName == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneName = "NA";
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
        }

        private void SendBackEndDropAction()
        {
            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.actionInfo.workLocationId = m_basicInfoModel.data[0].workLocationId;
            ActionObj.actionInfo.vehicleId = m_mainConfigModel.vehicleId;
            ActionObj.actionInfo.projectId = m_mainConfigModel.projectId;
            ActionObj.actionInfo.mappingId = m_mainConfigModel.mappingId;
            ActionObj.actionInfo.mapId = m_mainConfigModel.mapId;
            ActionObj.actionInfo.action = "drop";
            ActionObj.actionInfo.loadRate = "";
            ActionObj.actionInfo.loadWeight = 0;
            ActionObj.actionInfo.height = "";
            ActionObj.actionInfo.epc = "";
            ActionObj.actionInfo.cepc = "";
            ActionObj.actionInfo.loadId = "";
            ActionObj.actionInfo.shelf = false;
            ActionObj.actionInfo.loadMatrixRaw = "10";
            ActionObj.actionInfo.loadMatrixColumn = "10";
            ActionObj.actionInfo.visionWidth = 0;
            ActionObj.actionInfo.visionHeight = 0;
            ActionObj.actionInfo.visionDepth = 0;
            ActionObj.actionInfo.loadMatrix = [10, 10, 10];
            ActionObj.actionInfo.plMatrix = "";

            if (m_ActionZoneId == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneId = "NA";
            }
            else
            {
                ActionObj.actionInfo.zoneId = m_ActionZoneId;
            }


            if (m_ActionZoneName == null || m_ActionZoneId.Equals(""))
            {
                ActionObj.actionInfo.zoneName = "NA";
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
        }

        private void SendBackEndContainerGateEvent()
        {
            if (m_event_epc == "") return;
            if (m_set_load != true) return;

            ContainerGateEventModel model = new ContainerGateEventModel();
            model.containerInfo.vehicleId = m_mainConfigModel.vehicleId;
            model.containerInfo.projectId = m_mainConfigModel.projectId;
            model.containerInfo.mappingId = m_mainConfigModel.mappingId;
            model.containerInfo.mapId = m_mainConfigModel.mapId;
            model.containerInfo.cepc = m_event_epc;
            model.containerInfo.depc = m_event_epc;
            model.containerInfo.loadId = m_event_QRcode;

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
            if (m_weightModel.GrossWeight < 30)
            {
                m_Command = 0;
                m_isPickUp = false;
            }
            else
            {
                m_Command = 1;
                m_isPickUp = true;
            }
        }

        private void IsPickUpTimerEvent(object sender, EventArgs e)
        {
            try
            {
                // 픽업 판단 조건
                if (m_isPickUp == true) return;

                if (m_weight_list.Count < m_weight_sample_size) return;

                if (m_weightModel.GrossWeight < 30) return;

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

                    //// 부피, 형상 리복스 데이터 요청
                    //int getLivoxctn = 0;
                    //while (getLivoxctn < 100)
                    //{
                    //    SendToLivox(1);
                    //    if (GetSizeData() == true)
                    //    {
                    //        SendToLivox(0);
                    //        break;
                    //    }
                    //    getLivoxctn++;
                    //    Thread.Sleep(100);
                    //}

                    m_guideWeightStart = true;
                }

                //if (m_weight_list.Select(w => w.GrossWeight).Distinct().Count() > 4) return;

                int currentWeight = m_weightModel.GrossWeight;

                int minWeight = m_weight_list.Select(w => w.GrossWeight).Min();
                if (Math.Abs(currentWeight - minWeight) > currentWeight * 0.1) return;

                int maxWeight = m_weight_list.Select(w => w.GrossWeight).Max();
                if (Math.Abs(currentWeight - maxWeight) > currentWeight * 0.1) return;

                // 안정된 중량값, 부피값 할당
                m_event_weight = m_weightModel.GrossWeight;
                _eventAggregator.GetEvent<CallDataEvent>().Publish();


                // 중량값 측정 완료 LED, 부저
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

                if (m_stopwatch != null)
                {
                    m_stopwatch.Stop();
                    Tools.Log($"Stop -> Weight Check Complete : {m_stopwatch.ElapsedMilliseconds}ms", ELogType.ActionLog);
                }

                // 앱 물류 선택 X, QR 코드 X
                if (m_set_item == false && m_event_QRcode == "")
                {
                    //Pattlite_Buzzer_LED(ePlayBuzzerLed.NO_QR_PICKUP);
                    _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.qr_check_error);
                    //Thread.Sleep(200);
                }
                // 앱 물류 선택 X, QR 코드 O
                else if (m_set_item == false && m_event_QRcode.Contains("wata"))
                {
                    //Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_PIKCUP);
                    //Thread.Sleep(200);
                }
                // 앱 물류 선택 O, QR 코드 X
                else if (m_set_item == true && m_event_QRcode == "")
                {
                    //Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_PICKUP);
                    //Thread.Sleep(200);
                }
                // 앱 물류 선택 O, QR 코드 O
                else if (m_set_item == true && m_event_QRcode.Contains("wata"))
                {
                    //Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_PICKUP);
                    //Thread.Sleep(200);
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
            m_Command = 1;

            // 앱 물류 선택 X, QR 코드 X
            if (m_set_item == false && !m_event_QRcode.Contains("wata"))
            {
                //Pattlite_Buzzer_LED(ePlayBuzzerLed.NO_QR_MEASURE_OK);
                //_eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.weight_check_complete);
                //Thread.Sleep(500);
            }
            // 앱 물류 선택 X, QR 코드 O
            else if (m_set_item == false && m_event_QRcode.Contains("wata"))
            {
                //Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_MEASURE_OK);
                //_eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.weight_check_complete);
                //Thread.Sleep(500);
            }
            // 앱 물류 선택 O, QR 코드 X
            else if (m_set_item == true && !m_event_QRcode.Contains("wata"))
            {
                //Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_MEASURE_OK);
                //_eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.weight_check_complete);
                //Thread.Sleep(500);
            }
            // 앱 물류 선택 O, QR 코드 O
            else if (m_set_item == true && m_event_QRcode.Contains("wata"))
            {
                //Pattlite_Buzzer_LED(ePlayBuzzerLed.SET_ITEM_MEASURE_OK);
                //_eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.weight_check_complete);
                //Thread.Sleep(500);
            }

            CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, false);
            SendBackEndPickupAction();

            //로그
            Tools.Log($"Pickup Event!!! weight:{m_event_weight}kg, width:{m_event_width}, height:{m_event_height}, depth{m_event_length}", ELogType.ActionLog);
            Tools.Log($"Pickup Event!!! QR Code:{m_curr_QRcode}", ELogType.ActionLog);
        }

        private void IsDropTimerEvent(object sender, EventArgs e)
        {
            try
            {
                if (m_isPickUp == false)
                {
                    return;
                }

                if (m_weight_list.Count == 0 || m_weight_list == null)
                {
                    return;
                }

                if (m_weightModel.GrossWeight > 30)
                {
                    return;
                }
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

            // 중량값 리스트 초기화
            m_weight_list = new List<WeightSensorModel>();

            // 드롭 시 값 초기화
            m_event_weight = 0;
            //m_event_distance = 0;
            //m_event_QRcode = m_event_QRcode;
            //m_event_width = 0;
            //m_event_height = 0;
            //m_event_length = 0;
            m_event_points = "";

            // 부저 컨트롤
            Pattlite_Buzzer_LED(ePlayBuzzerLed.DROP);

            // m_stop_guide 초기화
            //m_distance_stop_guide = false;
            m_guideWeightStart = false;

            CalcDistanceAndGetZoneID(m_navModel.naviX, m_navModel.naviY, true);
            SendBackEndDropAction();

            // 드롭 직후 픽업이벤트 발생하는 것을 방지
            Thread.Sleep(1000);
        }
    }
}
