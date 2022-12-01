using Prism.Events;
using System;
using System.Runtime.Intrinsics.X86;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.VISION;

namespace WATA.LIS.Core.Services
{
    public class StatusService : IStatusService
    {
        IEventAggregator _eventAggregator;

        public int      m_Height_Distance_mm { get; set; }
        public string   m_RFID_EPC_SenorData { get; set; }

        private int rifid_status_check_count = 0;
        private int distance_status_check_count = 35;
        private int status_limit_count = 15;

        private string m_location = "INCHEON_CALT_001";
        private string m_vihicle = "fork_lift001";
        private string m_errorcode = "0000";

        

        public StatusService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RFIDSensorEvent>().Subscribe(OnRFIDSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<VISION_Event>().Subscribe(OnVISIONEvent, ThreadOption.BackgroundThread, true);

            DispatcherTimer StatusClearTimer = new DispatcherTimer();
            StatusClearTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            StatusClearTimer.Tick += new EventHandler(StatusClearEvent);
            StatusClearTimer.Start();

            DispatcherTimer AliveTimer = new DispatcherTimer();
            AliveTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            AliveTimer.Tick += new EventHandler(AliveTimerEvent);
            AliveTimer.Start();


            DispatcherTimer CurrentTimer = new DispatcherTimer();
            CurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            CurrentTimer.Tick += new EventHandler(CurrentLocationTimerEvent);
            CurrentTimer.Start();

        }

        private void CurrentLocationTimerEvent(object sender, EventArgs e)
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

        private void AliveTimerEvent(object sender, EventArgs e)
        {
            SendAliveEvent();
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

     



        private void StatusClearEvent(object sender, EventArgs e)
        {
            if(rifid_status_check_count >  status_limit_count)
            {
                RFIDSensorModel rfidmodel = new RFIDSensorModel();
                m_RFID_EPC_SenorData = "NA";
                Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
                rfidmodel.EPC_Data = m_RFID_EPC_SenorData;
                _eventAggregator.GetEvent<RFIDSensorEvent>().Publish(rfidmodel);
            }
            else
            {
                rifid_status_check_count++;
                Tools.Log($"Wait Count {rifid_status_check_count}", Tools.ELogType.SystemLog);
            }

            if (distance_status_check_count > status_limit_count)// 30초후 응답이 없으면 RFID 클리어
            {

                DistanceSensorModel DisTanceObject = new DistanceSensorModel();
                m_Height_Distance_mm = -100;
                DisTanceObject.Distance_mm = m_Height_Distance_mm;
                _eventAggregator.GetEvent<DistanceSensorEvent>().Publish(DisTanceObject);
                Tools.Log("#######Distance Status Clear #######", Tools.ELogType.SystemLog);
            }
            else
            {
                distance_status_check_count++;
            }


        }

        public void OnDistanceSensorData(DistanceSensorModel obj)
        {
            distance_status_check_count = 0;
            m_Height_Distance_mm = obj.Distance_mm;
            Tools.Log($"!! :  {m_Height_Distance_mm}", Tools.ELogType.SystemLog);   
        }

        public void OnRFIDSensorData(RFIDSensorModel obj)
        {
            rifid_status_check_count = 0;
            m_RFID_EPC_SenorData = obj.EPC_Data;
            Tools.Log($"!! :  {m_RFID_EPC_SenorData}", Tools.ELogType.SystemLog);
        }


        private int m_CalRate = 0;

        public void OnVISIONEvent(VISON_Model obj)
        {
            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.actionInfo.workLocationId = m_location;
            ActionObj.actionInfo.vehicleId = m_vihicle;
            ActionObj.actionInfo.epc = m_RFID_EPC_SenorData;
            Tools.Log($"##rftag epc  : {m_RFID_EPC_SenorData}", Tools.ELogType.BackEndLog);
            ActionObj.actionInfo.height = m_Height_Distance_mm.ToString();
            ActionObj.actionInfo.loadId = obj.qr;
            Tools.Log($"##########################pick up Action : {ActionObj.actionInfo.action}", Tools.ELogType.BackEndLog);

            if (obj.status == "pickup")//지게차가 물건을 올렸을경우 선반 에서는 물건이 빠질경우
            {
                ActionObj.actionInfo.action = "IN";
                m_CalRate = CalcLoadRate(obj.area);
                Tools.Log($"Area : {obj.area}", Tools.ELogType.BackEndLog);
                Tools.Log($"Copy Before LoadRate  : {m_CalRate}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.loadRate = "0";
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";
                for (int i = 0; i < 100; i++)
                {
                    ActionObj.actionInfo.loadMatrix.Add(0);
                }

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
         
                if (m_RFID_EPC_SenorData != "NA")
                {
                    _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
                }
                else
                {
                    Tools.Log("[do not Tray]Pick Up Empty EPC #######", Tools.ELogType.BackEndLog);
                }
            }
            else if(obj.status == "drop")//지게차가 물건을 놨을경우  선반 에서는 물건이 추가될 경우
            {
                ActionObj.actionInfo.action = "OUT";
                ActionObj.actionInfo.loadRate = m_CalRate.ToString();
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";
                for (int i = 0; i < 100; i++)
                {
                    ActionObj.actionInfo.loadMatrix.Add(1);
                }

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;

                Tools.Log($"##rftag epc  : {m_RFID_EPC_SenorData}", Tools.ELogType.BackEndLog);

                if (m_RFID_EPC_SenorData != "NA")
                {
                    Tools.Log($"##LoadRate  : {ActionObj.actionInfo.loadRate}", Tools.ELogType.BackEndLog);
                    _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
                }
                else
                {
                    Tools.Log("[do not Tray]Drop Empty EPC #######", Tools.ELogType.BackEndLog);
                }
                m_RFID_EPC_SenorData = "NA";
                Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
                m_CalRate = 0;
                Tools.Log("Clear LodRate", Tools.ELogType.BackEndLog);
            }
            else
            {
                Tools.Log("Action Idle", Tools.ELogType.BackEndLog);
            }
        }

        private  int  CalcLoadRate(float area)
        {
            Tools.Log($"##area  : {area}", Tools.ELogType.BackEndLog);
            double nRet = (area / 0.99) * 100;
        
            if (nRet <= 0)
            {
                 nRet = 0;
            }
            if(nRet>= 97)
            {
                nRet = 97;
            }
            Tools.Log($"##Rate  : {nRet}", Tools.ELogType.BackEndLog);


            return (int)nRet;
        }
    }
}
