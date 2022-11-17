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
            alive_obj.alive.workLocationId = m_location;
            alive_obj.alive.vehicleId = m_vihicle;
            alive_obj.alive.errorCode = m_errorcode;
            string json_body = Util.ObjectToJson(alive_obj);
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

        public void OnVISIONEvent(VISON_Model obj)
        {
            bool IsSendBackEnd = false;


            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.actionInfo.workLocationId = m_location;




            if (obj.status == "pickup")
            {
                ActionObj.actionInfo.action = "IN";

            }
            else if(obj.status == "drop")
            {
                ActionObj.actionInfo.action = "OUT";
            }
            else
            {
                ActionObj.actionInfo.action = "N/A";
            }

            if ((obj.status.Contains("drop") || obj.status.Contains("pickup")) && m_RFID_EPC_SenorData != "NA")
            {
                IsSendBackEnd = true;
            }
            else
            {
                IsSendBackEnd = false;
            }


            ActionObj.actionInfo.vehicleId = m_vihicle;
            ActionObj.actionInfo.epc = m_RFID_EPC_SenorData;
            ActionObj.actionInfo.height = m_Height_Distance_mm.ToString();
            ActionObj.actionInfo.loadId = obj.qr;



            int CalRate = CalcLoadRate(obj.area);
            Tools.Log($"Area : {obj.area}", Tools.ELogType.BackEndLog);

            ActionObj.actionInfo.loadRate = CalRate.ToString();
            ActionObj.actionInfo.loadMatrixRaw = "10";
            ActionObj.actionInfo.loadMatrixColumn = "10";


           

            if (CalRate > 0)
            {

                for (int i = 0; i < 100; i++)
                {
                    ActionObj.actionInfo.loadMatrix.Add(1);
                }   
            }
            else
            {
                for (int i = 0; i < 100; i++)
                {
                    ActionObj.actionInfo.loadMatrix.Add(0);
                }

            }

            string json_body = Util.ObjectToJson(ActionObj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            post_obj.body = json_body;


            if (IsSendBackEnd == true)
            {
                Tools.Log($"Action : {ActionObj.actionInfo.action}", Tools.ELogType.BackEndLog);
                Tools.Log($"LoadRate  : {ActionObj.actionInfo.loadRate}", Tools.ELogType.BackEndLog);


                Tools.Log("Send Action Event", Tools.ELogType.SystemLog);
                _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
            }
            else
            {

            }
             
        }

        private  int  CalcLoadRate(float area)
        {
            int nRet = 0;
            nRet = (int)(area / 0.99) * 100;


            if(nRet < 30)
            {
                nRet = 0;
            }
            return nRet;
        }
    }
}
