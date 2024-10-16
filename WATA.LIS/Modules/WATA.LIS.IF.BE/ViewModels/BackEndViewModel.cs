using log4net.Core;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.LIVOX;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.LIVOX;
using WATA.LIS.Core.Model.VISION;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Model.Indicator;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Model.VisionCam;
using WATA.LIS.Core.Services;
using Windows.ApplicationModel.UserDataTasks;
using Windows.Devices.Bluetooth.Advertisement;
using static System.Net.WebRequestMethods;
using WATA.LIS.Core.Interfaces;
using OpenCvSharp;
using WATA.LIS.Core.Model.SystemConfig;
using System.IO;
using File = System.IO.File;
using WATA.LIS.Core.Events.WeightSensor;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.IF.BE.ViewModels
{
    public class BackEndViewModel : BindableBase
    {
        public ObservableCollection<Log> ListBackEndLog { get; set; }
        public ObservableCollection<Log> ListBackEndCurrentLog { get; set; }
        public DelegateCommand<string> ButtonFunc { get; set; }

        private readonly IEventAggregator _eventAggregator;

        private string m_location = "INCHEON_CALT_001";
        private string m_vihicle = "fork_lift001";
        private string m_errorcode = "0000";


        private string _TagInfo;
        public string TagInfo { get { return _TagInfo; } set { SetProperty(ref _TagInfo, value); } }


        private string _DistanceInfo;
        public string DistanceInfo { get { return _DistanceInfo; } set { SetProperty(ref _DistanceInfo, value); } }



        private string _QRLoadID;
        public string QRLoadID { get { return _QRLoadID; } set { SetProperty(ref _QRLoadID, value); } }




        private string _QRInfo;
        public string QRInfo { get { return _QRInfo; } set { SetProperty(ref _QRInfo, value); } }

        DispatcherTimer _JobTimer1;
        DispatcherTimer _JobTimer2;
        DispatcherTimer _JobTimer3;
        DispatcherTimer _JobTimer4;


        // Livox
        private LIVOXModel m_livoxModel = new LIVOXModel();
        private float m_livox_width = 0;
        private float m_livox_height = 0;
        private float m_livox_depth = 0;
        private string m_livox_points = "";

        PublisherSocket _publisherSocket;
        SubscriberSocket _subscriberSocket;

        // Indicator
        IndicatorModel m_indicatorModel = new IndicatorModel();
        DispatcherTimer _indicatorTimer = new DispatcherTimer();
        private int _mCommand = 0;


        //VisonCam
        VisionCamModel m_visoncammodel = new VisionCamModel();
        private string m_lastQRcode = "";

        // 중량센서 모델
        private WeightSensorModel m_weightModel;
        private List<WeightSensorModel> m_weight_list = new List<WeightSensorModel>();
        private const int m_weight_sample_size = 50;
        private int m_event_weight = 0;



        // 테스트용 로그 파일 경로
        private int m_weight = 100;
        private readonly string _weightLogFilePath;



        public BackEndViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            ListBackEndLog = Tools.logInfo.ListBackEndLog;
            ListBackEndCurrentLog = Tools.logInfo.ListBackEndCurrentLog;

            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);
            Tools.Log($"##########################Init", Tools.ELogType.BackEndLog);
            Tools.Log($"##########################Init", Tools.ELogType.BackEndCurrentLog);
            TagInfo = "DC4353495520008203224731";


            //_JobTimer1 = new DispatcherTimer();
            //_JobTimer1.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //_JobTimer1.Tick += new EventHandler(JobEvent1);

            //_JobTimer2 = new DispatcherTimer();
            //_JobTimer2.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //_JobTimer2.Tick += new EventHandler(JobEvent2);

            //_JobTimer3 = new DispatcherTimer();
            //_JobTimer3.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //_JobTimer3.Tick += new EventHandler(JobEvent3);

            //_JobTimer4 = new DispatcherTimer();
            //_JobTimer4.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //_JobTimer4.Tick += new EventHandler(JobEvent4);

            // Livox
            _eventAggregator.GetEvent<LIVOXEvent>().Subscribe(OnLivoxSensorEvent, ThreadOption.BackgroundThread, true);

            // Indicator
            _eventAggregator.GetEvent<IndicatorRecvEvent>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);

            _indicatorTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            _indicatorTimer.Tick += new EventHandler(IndicatorSendTimerEvent);
            _indicatorTimer.Start();

            // VisonCam
            //_eventAggregator.GetEvent<>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<HikVisionEvent>().Subscribe(OnVisionEvent, ThreadOption.BackgroundThread, true);

            _eventAggregator.GetEvent<WeightSensorEvent>().Subscribe(onWeightEvent, ThreadOption.BackgroundThread, true);

            m_weightModel = new WeightSensorModel();


            InitLivox();


            //// 프로그램 시작 경로에 weight_log.txt 파일 경로를 설정합니다.
            //_weightLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_weight_log.txt");

            //// 프로그램 시작 시 로그 파일에서 _m_weight 값을 읽어옵니다.
            //m_weight = ReadWeightFromLog();

        }

        private int _jobcnt1 = 0;
        private int _jobcnt2 = 0;
        private int _jobcnt3 = 0;
        private int _jobcnt4 = 0;


        private void JobEvent1(object sender, EventArgs e)
        {

            SimulationModel sim_start = new SimulationModel();
            if (_jobcnt1 == 0)
            {
                Tools.Log($"##########################Senario 1 Start", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 1 End", Tools.ELogType.BackEndCurrentLog);

                sim_start.EPC = "filed";
                sim_start.QR = "watad7d7a690ecbb4b3090102f88605f9b5e";
                sim_start.IS_SIMULATION = true;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
            }
            else if (_jobcnt1 == 10)
            {
                VISON_Model pickup_vision = new VISON_Model();
                pickup_vision.area = 100;
                pickup_vision.width = 100;
                pickup_vision.height = 100;
                pickup_vision.depth = 100;
                pickup_vision.qr = "watad7d7a690ecbb4b3090102f88605f9b5e";
                pickup_vision.status = "pickup";
                byte[] matrix_temp = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                pickup_vision.matrix = matrix_temp;
                pickup_vision.has_roof = false;
                _eventAggregator.GetEvent<VISION_Event>().Publish(pickup_vision);
            }
            else if (_jobcnt1 == 20)
            {
                sim_start.EPC = "DC4353495520008203224731";
                sim_start.QR = "watad7d7a690ecbb4b3090102f88605f9b5e";
                sim_start.IS_SIMULATION = true;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);

            }
            else if (_jobcnt1 == 30)
            {

                VISON_Model drop_vision = new VISON_Model();
                drop_vision.area = 100;
                drop_vision.width = 100;
                drop_vision.height = 100;
                drop_vision.depth = 100;
                drop_vision.qr = "watad7d7a690ecbb4b3090102f88605f9b5e";
                drop_vision.status = "drop";
                byte[] matrix_temp1 = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                drop_vision.matrix = matrix_temp1;
                drop_vision.has_roof = false;
                _eventAggregator.GetEvent<VISION_Event>().Publish(drop_vision);


            }
            else if (_jobcnt1 == 40)
            {
                sim_start.IS_SIMULATION = false;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
                _jobcnt1 = 0;
                _JobTimer1.Stop();
                Tools.Log($"##########################Senario 1 End", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 1 End", Tools.ELogType.BackEndCurrentLog);
            }


            _jobcnt1++;
        }


        private void JobEvent2(object sender, EventArgs e)
        {

            SimulationModel sim_start = new SimulationModel();
            if (_jobcnt2 == 0)
            {
                Tools.Log($"##########################Senario 2 Start", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 2 End", Tools.ELogType.BackEndCurrentLog);

                sim_start.EPC = "filed";
                sim_start.QR = "NA";
                sim_start.IS_SIMULATION = true;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
            }
            else if (_jobcnt2 == 10)
            {
                VISON_Model pickup_vision = new VISON_Model();
                pickup_vision.area = 100;
                pickup_vision.width = 100;
                pickup_vision.height = 100;
                pickup_vision.depth = 100;
                pickup_vision.qr = "NA";
                pickup_vision.status = "pickup";
                byte[] matrix_temp = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                pickup_vision.matrix = matrix_temp;
                pickup_vision.has_roof = false;
                _eventAggregator.GetEvent<VISION_Event>().Publish(pickup_vision);
            }
            else if (_jobcnt2 == 20)
            {
                sim_start.IS_SIMULATION = false;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
                _jobcnt1 = 0;
                _JobTimer2.Stop();
                Tools.Log($"##########################Senario 2 End", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 2 End", Tools.ELogType.BackEndCurrentLog);
            }
            _jobcnt2++;
        }


        private void JobEvent3(object sender, EventArgs e)
        {

            SimulationModel sim_start = new SimulationModel();
            if (_jobcnt3 == 0)
            {
                Tools.Log($"##########################Senario 3 Start", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 3 End", Tools.ELogType.BackEndCurrentLog);

                sim_start.EPC = "filed";
                sim_start.QR = "error_qr_data";
                sim_start.IS_SIMULATION = true;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
            }
            else if (_jobcnt3 == 10)
            {
                VISON_Model pickup_vision = new VISON_Model();
                pickup_vision.area = 100;
                pickup_vision.width = 100;
                pickup_vision.height = 100;
                pickup_vision.depth = 100;
                pickup_vision.qr = "error_qr_data";
                pickup_vision.status = "pickup";
                byte[] matrix_temp = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                pickup_vision.matrix = matrix_temp;
                pickup_vision.has_roof = false;
                _eventAggregator.GetEvent<VISION_Event>().Publish(pickup_vision);
            }
            else if (_jobcnt3 == 20)
            {
                sim_start.EPC = "DC4353495520008203224731";
                sim_start.QR = "error_qr_data";
                sim_start.IS_SIMULATION = true;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);

            }
            else if (_jobcnt3 == 30)
            {
                sim_start.IS_SIMULATION = false;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
                _jobcnt3 = 0;
                _JobTimer3.Stop();
                Tools.Log($"##########################Senario 3 End", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 3 End", Tools.ELogType.BackEndCurrentLog);
            }


            _jobcnt3++;
        }

        private void JobEvent4(object sender, EventArgs e)
        {

            SimulationModel sim_start = new SimulationModel();
            if (_jobcnt4 == 0)
            {
                Tools.Log($"##########################Senario 4 Start", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 4 End", Tools.ELogType.BackEndCurrentLog);
                sim_start.EPC = "filed";
                sim_start.QR = "watad7d7a690ecbb4b3090102f88605f9b5e";
                sim_start.IS_SIMULATION = true;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
            }
            else if (_jobcnt4 == 10)
            {
                VISON_Model pickup_vision = new VISON_Model();
                pickup_vision.area = 100;
                pickup_vision.width = 100;
                pickup_vision.height = 100;
                pickup_vision.depth = 100;
                pickup_vision.qr = "NA";
                pickup_vision.status = "pickup";
                byte[] matrix_temp = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                pickup_vision.matrix = matrix_temp;
                pickup_vision.has_roof = false;
                _eventAggregator.GetEvent<VISION_Event>().Publish(pickup_vision);
            }
            else if (_jobcnt4 == 20)
            {
                sim_start.EPC = "DC4353495520008203224733";
                sim_start.QR = "watad7d7a690ecbb4b3090102f88605f9b5e";
                sim_start.IS_SIMULATION = true;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);

            }

            else if (_jobcnt4 == 30)
            {
                sim_start.IS_SIMULATION = false;
                _eventAggregator.GetEvent<SimulModeEvent>().Publish(sim_start);
                _jobcnt4 = 0;
                _JobTimer4.Stop();
                Tools.Log($"##########################Senario 4 End", Tools.ELogType.BackEndLog);
                Tools.Log($"##########################Senario 4 End", Tools.ELogType.BackEndCurrentLog);
            }
            _jobcnt4++;
        }




        public void SendAliveEvent()
        {
            AliveModel alive_obj = new AliveModel();
            alive_obj.alive.workLocationId = m_location;
            alive_obj.alive.vehicleId = m_vihicle;
            alive_obj.alive.errorCode = m_errorcode;
            string json_body = Util.ObjectToJson(alive_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/alive";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndCurrent;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        private void SendAction_IN_InfoEvent(string Tag, string Distance, bool Shelf, string loadid)
        {
            ActionInfoModel action_obj = new ActionInfoModel();
            action_obj.actionInfo.workLocationId = m_location;
            action_obj.actionInfo.vehicleId = m_vihicle;
            action_obj.actionInfo.loadId = loadid;
            action_obj.actionInfo.action = "IN";
            action_obj.actionInfo.epc = Tag;
            action_obj.actionInfo.height = Distance;
            action_obj.actionInfo.loadRate = "0";
            action_obj.actionInfo.loadMatrixColumn = "10";
            action_obj.actionInfo.loadMatrixRaw = "10";
            action_obj.actionInfo.shelf = Shelf;

            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);


            string json_body = Util.ObjectToJson(action_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);


        }

        private void SendAction_OUT_InfoEvent(string Tag, string Distance, bool Shelf, string loadid)
        {


            ActionInfoModel action_obj = new ActionInfoModel();
            action_obj.actionInfo.workLocationId = m_location;
            action_obj.actionInfo.vehicleId = m_vihicle;

            action_obj.actionInfo.loadId = loadid;


            action_obj.actionInfo.action = "OUT";
            action_obj.actionInfo.epc = Tag;
            action_obj.actionInfo.height = Distance;
            action_obj.actionInfo.loadRate = "";
            action_obj.actionInfo.loadRate = "90";
            action_obj.actionInfo.loadMatrixColumn = "10";
            action_obj.actionInfo.loadMatrixRaw = "10";
            action_obj.actionInfo.shelf = Shelf;


            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(0);



            string json_body = Util.ObjectToJson(action_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        private void SendLocationInfoEvent(string Tag)
        {
            LocationInfoModel location_obj = new LocationInfoModel();

            location_obj.locationInfo.vehicleId = m_vihicle;
            location_obj.locationInfo.workLocationId = m_location;
            location_obj.locationInfo.epc = Tag;

            string json_body = Util.ObjectToJson(location_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/location";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        private void ButtonFuncClick(string command)
        {
            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "Get":

                        SendAliveEvent();

                        break;

                    case "GateIN":
                        GateAction(eGateActionType.IN);
                        break;

                    case "GateOUT":
                        GateAction(eGateActionType.OUT);
                        break;


                    case "ActionIN": //4.

                        VISON_Model visionModel4 = new VISON_Model();
                        visionModel4.area = 100;
                        visionModel4.width = 100;
                        visionModel4.height = 100;
                        visionModel4.depth = 100;
                        visionModel4.qr = "NA";
                        visionModel4.status = "drop";
                        byte[] _LoadMatrix4 = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                        visionModel4.matrix = _LoadMatrix4;
                        visionModel4.simulation_status = "IN";
                        _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel4);

                        break;

                    case "ActionOUT": //3.



                        VISON_Model visionModel3 = new VISON_Model();
                        visionModel3.area = 100;
                        visionModel3.width = 100;
                        visionModel3.height = 100;
                        visionModel3.depth = 100;
                        visionModel3.qr = "NA";
                        visionModel3.status = "pickup";
                        byte[] _LoadMatrix3 = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                        visionModel3.matrix = _LoadMatrix3;
                        visionModel3.has_roof = false;
                        visionModel3.simulation_status = "OUT";
                        _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel3);

                        break;

                    case "F_IN":  //2.

                        VISON_Model visionModel2 = new VISON_Model();
                        visionModel2.area = 100;
                        visionModel2.width = 100;
                        visionModel2.height = 100;
                        visionModel2.depth = 100;
                        visionModel2.qr = "NA";
                        visionModel2.status = "drop";
                        byte[] _LoadMatrix2 = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                        visionModel2.matrix = _LoadMatrix2;
                        visionModel2.has_roof = false;
                        visionModel2.simulation_status = "F_IN"; ;
                        _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel2);


                        break;

                    case "F_OUT": //1.



                        VISON_Model visionModel = new VISON_Model();
                        visionModel.area = 100;
                        visionModel.width = 100;
                        visionModel.height = 100;
                        visionModel.depth = 100;
                        visionModel.qr = "NA";
                        visionModel.status = "pickup";
                        byte[] _LoadMatrix = new byte[10] { 9, 10, 10, 10, 10, 10, 10, 10, 10, 9 };
                        visionModel.matrix = _LoadMatrix;
                        visionModel.has_roof = false;
                        visionModel.simulation_status = "F_OUT";
                        _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel);

                        break;

                    case "Location":

                        SendLocationInfoEvent("DA00025C00020000000200ED");

                        break;


                    case "Container_Send":

                        Container(QRInfo);


                        break;


                    case "CTR_1":
                        {

                            _JobTimer1.Start();
                            break;
                        }

                    case "CTR_2":
                        {
                            _JobTimer2.Start();
                            break;
                        }


                    case "CTR_3":
                        {
                            _JobTimer3.Start();
                            break;
                        }


                    case "CTR_4":
                        {
                            _JobTimer4.Start();
                            break;
                        }


                    case "PICKUP":
                        {
                            // Livox part
                            SendToLivox(1);

                            _mCommand = 1;
                            if (GetSizeData() == true)
                            {
                                SendToLivox(0);
                                return;
                            }

                            // Indicator Part
                            SendPickUpEvent();

                            break;
                        }


                    case "DROP":
                        {
                            // Indicator Part
                            SendDropEvent();
                            break;
                        }


                    default:
                        break;
                }
            }
            catch (Exception ex)
            {

            }
        }


        private void Container(string qr)
        {
            ContainerGateEventModel GateEventModelobj = new ContainerGateEventModel();
            GateEventModelobj.containerInfo.vehicleId = m_vihicle;
            GateEventModelobj.containerInfo.cepc = TagInfo;
            GateEventModelobj.containerInfo.loadId = qr;
            string json_body = Util.ObjectToJson(GateEventModelobj);
            RestClientPostModel post_obj = new RestClientPostModel();

            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndContainer;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/container-gate-event";

            Tools.Log($"URL : {post_obj.url} ", Tools.ELogType.BackEndLog);

            Tools.Log($"Body : {json_body} ", Tools.ELogType.BackEndLog);
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);
        }

        public void GateAction(eGateActionType action)
        {
            GateEventModel ActionObj = new GateEventModel();

            ActionObj.gateEvent.workLocationId = m_location;
            ActionObj.gateEvent.vehicleId = m_vihicle;
            ActionObj.gateEvent.getLocation = "room1";

            if (action == eGateActionType.IN)
            {
                ActionObj.gateEvent.eventType = "IN";
            }
            else
            {
                ActionObj.gateEvent.eventType = "OUT";
            }

            string json_body = Util.ObjectToJson(ActionObj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/gate-event";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }


        /// <summary>
        /// Pickup Event Test
        /// </summary>
        /// <param name="commandNum"></param>
        private void OnLivoxSensorEvent(LIVOXModel LivoxSensorModel)
        {
            m_livoxModel = LivoxSensorModel;
        }

        private void InitLivox()
        {
            try
            {
                _publisherSocket = new PublisherSocket();
                // 퍼블리셔 소켓을 5555 포트에 바인딩합니다.
                _publisherSocket.Bind("tcp://127.0.0.1:5002");

                Tools.Log($"InitLivox", Tools.ELogType.BackEndLog);

                _subscriberSocket = new SubscriberSocket();
                // 서브스크라이버 소켓을 5555 포트에 연결합니다.
                _subscriberSocket.Connect("tcp://127.0.0.1:5001");

                // 타임아웃 설정 (예: 5초)
                _subscriberSocket.Options.HeartbeatTimeout = TimeSpan.FromSeconds(5);
            }
            catch (Exception ex)
            {
                Tools.Log($"Failed InitLivox : {ex.Message}", Tools.ELogType.BackEndLog);
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
                //await Task.Run(() => {

                //});

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

                        LIVOXModel eventModel = new LIVOXModel();
                        eventModel.topic = "MID360>LIS";
                        eventModel.responseCode = 0;
                        eventModel.height = (int)jsonObject["height"];
                        eventModel.width = (int)jsonObject["width"];
                        eventModel.length = (int)jsonObject["length"];
                        eventModel.result = (int)jsonObject["result"]; // bool 값을 int로 변환
                        eventModel.points = jsonObject["points"].ToString();

                        m_livox_height = eventModel.height;
                        m_livox_width = eventModel.width;
                        m_livox_depth = eventModel.length;
                        m_livox_points = eventModel.points;

                        _eventAggregator.GetEvent<LIVOXEvent>().Publish(eventModel);
                        Tools.Log($"height:{m_livox_height}, width:{m_livox_width}, depth:{m_livox_depth}", Tools.ELogType.BackEndLog);

                        return ret = true;
                    }
                    else
                    {
                        // 부피사이즈를 읽어오지 못했을 때 처리
                        m_livox_height = -1;
                        m_livox_width = -1;
                        m_livox_depth = -1;
                        m_livox_points = "";
                    }
                }
                else
                {
                    // 타임아웃 발생 시 처리
                    m_livox_height = -1;
                    m_livox_width = -1;
                    m_livox_depth = -1;
                    m_livox_points = "";

                    SendToLivox(1);
                    Tools.Log("Timeout occurred while receiving message", Tools.ELogType.BackEndLog);
                }
            }
            catch (Exception ex)
            {
                // 예외 처리
                SendToLivox(1);
                Tools.Log($"Exception occurred: {ex.Message}", Tools.ELogType.BackEndLog);
            }

            return ret;
        }


        /// <summary>
        /// Indicator 
        /// </summary>
        /// <param name="obj"></param>
        /// <exception cref="NotImplementedException"></exception>

        private void OnIndicatorEvent(string status)
        {
            Tools.Log($"OnIndicatorEvent {status}", Tools.ELogType.DisplayLog);

            // 통합 모니터링
            if (status == "start_unload")
            {
                //m_is_unload = true;
            }

            if (status == "stop_unload")
            {
                //m_is_unload = false;
            }


            //판토스 인디케이터
            if (status == "set_load")
            {
                Tools.Log(status, Tools.ELogType.BackEndLog);
            }

            if (status == "set_unload")
            {
                Tools.Log(status, Tools.ELogType.BackEndLog);
            }

            if (status == "set_normal")
            {
                SendDropEvent();
                Tools.Log(status, Tools.ELogType.BackEndLog);
            }
        }

        private void IndicatorSendTimerEvent(object sender, EventArgs e)
        {
            IndicatorModel Model = new IndicatorModel();
            Model.forklift_status.command = _mCommand;
            Model.forklift_status.QR = m_lastQRcode;
            Model.forklift_status.weightTotal = m_weightModel.GrossWeight;
            Model.forklift_status.visionHeight = m_livox_height;
            Model.forklift_status.visionWidth = m_livox_width;
            Model.forklift_status.visionDepth = m_livox_depth;
            Model.forklift_status.points = m_livox_points;
            Model.forklift_status.epc = "";
            Model.forklift_status.networkStatus = true;
            Model.forklift_status.weightSensorStatus = true;
            Model.forklift_status.visionCamStatus = true;
            Model.forklift_status.lidar2dStatus = true;
            Model.forklift_status.lidar3dStatus = true;
            Model.forklift_status.heightSensorStatus = true;
            Model.forklift_status.rfidStatus = true;

            string json_body = Util.ObjectToJson(Model);
            _eventAggregator.GetEvent<IndicatorSendEvent>().Publish(json_body);
            Tools.Log($" Send Command : {_mCommand}, weight:{Model.forklift_status.weightTotal}, width:{m_livox_width}, height:{m_livox_height}, depth:{m_livox_depth}", Tools.ELogType.BackEndLog);
        }

        private void SendPickUpEvent()
        {
            // _m_weight 값을 1 증가시키고 로그에 남깁니다.

            Tools.Log($" Send Command : {_mCommand}, width:{m_livox_width}, height:{m_livox_height}, depth:{m_livox_depth}", Tools.ELogType.BackEndLog);
        }

        private void SendDropEvent()
        {
            _mCommand = 0;
            m_livox_height = 0;
            m_livox_width = 0;
            m_livox_depth = 0;

            Tools.Log($"Send Command : {_mCommand}", Tools.ELogType.BackEndLog);
        }


        /// <summary>
        /// 테스트용 중량 로그 파일 경로
        /// </summary>
        /// <param name="weight"></param>
        private void onWeightEvent(WeightSensorModel obj)
        {
            m_weight_list.Add(obj);

            if (m_weight_list.Count >= m_weight_sample_size)
            {
                m_weightModel.LeftWeight = GetStableValue(m_weight_list.Select(w => w.LeftWeight).ToList());
                m_weightModel.RightWeight = GetStableValue(m_weight_list.Select(w => w.RightWeight).ToList());
                m_weightModel.GrossWeight = GetStableValue(m_weight_list.Select(w => w.GrossWeight).ToList());

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

        private void OnVisionEvent(VisionCamModel model)
        {
            m_visoncammodel = model;
            if (m_visoncammodel.QR.Contains("wata"))
            {
                m_lastQRcode = m_visoncammodel.QR;
            }
        }
    }
}
