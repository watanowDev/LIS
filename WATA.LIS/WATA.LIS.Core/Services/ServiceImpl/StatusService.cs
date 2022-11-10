using Prism.Events;
using System;
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
        private int vision_status_check_count = 35;
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
        }
        private void AliveTimerEvent(object sender, EventArgs e)
        {
            SendAliveEvent();
        }

        public void SendAliveEvent()
        {
            AliveModel alive_obj = new AliveModel();
            alive_obj.alive.Work_Location_ID = m_location;
            alive_obj.alive.Vehicle_ID = m_vihicle;
            alive_obj.alive.ErrorCode = m_errorcode;
            string json_body = Util.ObjectToJson(alive_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "http://192.168.0.1";
            post_obj.body = json_body;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        public void SendActionInfoEvent()
        {
            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.ActionInfo.Work_Location_ID = m_location;
            ActionObj.ActionInfo.Vehicle_ID = m_vihicle;
            ActionObj.ActionInfo.Load_Rate = "100";
            string json_body = Util.ObjectToJson(ActionObj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "http://192.168.0.1";
            post_obj.body = json_body;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }



        private void StatusClearEvent(object sender, EventArgs e)
        {
            if(rifid_status_check_count >  status_limit_count)// 30초후 응답이 없으면 RFID 클리어
            {
                m_RFID_EPC_SenorData = "NA";
                RFIDSensorModel rfidmodel = new RFIDSensorModel();
                rfidmodel.EPC_Data = m_RFID_EPC_SenorData;
                _eventAggregator.GetEvent<RFIDSensorEvent>().Publish(rfidmodel);
                Tools.Log("#######rifid Status Clear #######", Tools.ELogType.SystemLog);
            }
            else
            {
                rifid_status_check_count++;
                Tools.Log($"#######rifid Status Clear ####### Count {rifid_status_check_count}", Tools.ELogType.SystemLog);
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

        public void OnVISIONEvent(VISON_Model obj)
        {
            Tools.Log("OnVISIONEvent {}", Tools.ELogType.SystemLog);   
        }
    }
}
