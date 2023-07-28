using MaterialDesignThemes.Wpf;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Windows.Media.Media3D;
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
    /*
     * StatusService_V1 구성요소
     * 거리센서 : TeraBee EVO-60 (높이 측정)
     * RFID : FH920 (중국산 RFID 수신기)
     * VISION : Astra-S, Astra-Plus
     */

    public class StatusService_V1 : IStatusService
    {
        IEventAggregator _eventAggregator;

        public int      m_Height_Distance_mm { get; set; }
 
        private int rifid_status_check_count = 0;
        private int distance_status_check_count = 35;
        private int status_limit_count = 8;

        private string m_location = "INCHEON_CALT_001";
        private string m_vihicle = "fork_lift001";
        private string m_errorcode = "0000";

        

        public StatusService_V1(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RackProcess_Event>().Subscribe(OnRFIDSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<LocationProcess_Event>().Subscribe(OnLocationData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<VISION_Event>().Subscribe(OnVISIONEvent, ThreadOption.BackgroundThread, true);

            DispatcherTimer StatusClearTimer = new DispatcherTimer();
            StatusClearTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            StatusClearTimer.Tick += new EventHandler(StatusClearEvent);
            StatusClearTimer.Start();

            DispatcherTimer CurrentTimer = new DispatcherTimer();
            CurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            CurrentTimer.Tick += new EventHandler(CurrentLocationTimerEvent);
            CurrentTimer.Start();


        }

        private void CurrentLocationTimerEvent(object sender, EventArgs e)
        {
            LocationInfoModel location_obj = new LocationInfoModel();
            location_obj.locationInfo.vehicleId = m_vihicle;
            location_obj.locationInfo.workLocationId = m_location;
            location_obj.locationInfo.epc = m_Location_epc;
            string json_body = Util.ObjectToJson(location_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/location";
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
                RackRFIDEventModel rfidmodel = new RackRFIDEventModel();
                ClearEpc();
                Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
                rfidmodel.EPC = "NA";
                _eventAggregator.GetEvent<RackProcess_Event>().Publish(rfidmodel);
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

        private static List<QueryRFIDModel> m_epclist = new List<QueryRFIDModel>();
       
        private void AddEpcList(string epc , 
                                ref Dictionary<string, int> dicEPClist, 
                                ref List<int> listCount)
        {
            if (dicEPClist.ContainsKey(epc))
            {
                int idx = Array.IndexOf(dicEPClist.Keys.ToArray(), epc);
                listCount[idx] ++;
                dicEPClist[epc] = listCount[idx];
            }
            else
            {
                dicEPClist.Add(epc, 1);
                listCount.Add(1);

            }
        }

        private string GetMostCountEPC(int TimeSec,int Threshold)
        {
            string retKeys = "NA";
           
            if (m_epclist.Count > 0)
            {
                DateTime CurrentTime = DateTime.Now;
                Tools.Log($"Current Time {CurrentTime}  ", Tools.ELogType.BackEndLog);
                for (int i = 0; i < m_epclist.Count; i++)
                {

                    
                }

                int idx = 0;
                while (idx < m_epclist.Count)
                {
                    TimeSpan Diff = CurrentTime - m_epclist[idx].Time;
                    int nDiff = Diff.Seconds;


                    if (nDiff > TimeSec)
                    {
                        Tools.Log($"delete DiffTime {nDiff}  epc {m_epclist[idx].EPC} Time {m_epclist[idx].Time}  ", Tools.ELogType.BackEndLog);
                        m_epclist.Remove(m_epclist[idx]);

                    }
                    else
                    {
                        ++idx;
                    }
                }




                Dictionary<string, int> dicEPClist = new Dictionary<string, int>();
                List<int> listCount = new List<int>();


                for (int i = 0; i < m_epclist.Count; i++)
                {
                    Tools.Log($"Query  epc {m_epclist[i].EPC} Time {m_epclist[i].Time}  ", Tools.ELogType.BackEndLog);
                    AddEpcList(m_epclist[i].EPC, ref dicEPClist, ref listCount);
                }

                if (dicEPClist.Count > 0)
                {
                    PrintDict(dicEPClist);
                    retKeys = dicEPClist.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;

                    if (dicEPClist.TryGetValue(retKeys, out int Count))
                    {
                        Tools.Log($"EPCKey Count {Count}", Tools.ELogType.BackEndLog);
                        if (Count < Threshold)
                        {
                            retKeys = "NA";
                            Tools.Log($"Low Count {Count}", Tools.ELogType.BackEndLog);
                        }
                    }
                }
                else
                {
                    Tools.Log("Dic List Empty", Tools.ELogType.BackEndLog);
                }
            }
            else
            {
                Tools.Log("EPC List Empty", Tools.ELogType.BackEndLog);
            }
            return retKeys;
        }

        private void ClearEpc()
        {
            m_epclist.Clear();
            Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
        }

        private string m_Location_epc = "";

        public void OnLocationData(LocationRFIDEventModel obj)
        {
            m_Location_epc = obj.EPC;
            Tools.Log($"Location EPC {m_Location_epc}", Tools.ELogType.SystemLog);
        }

        public void OnRFIDSensorData(RackRFIDEventModel obj)
        {
            rifid_status_check_count = 0;
            QueryRFIDModel epcModel = new QueryRFIDModel();
            epcModel.EPC = obj.EPC;
            epcModel.Time = DateTime.Now;
            m_epclist.Add(epcModel);

            Tools.Log($"EPC Recieve :  {obj.EPC}", Tools.ELogType.SystemLog);
        }


        private int m_CalRate = 0;
        private byte[] m_LoadMatrix = new byte[10];

        private string m_qr = "";
        public void OnVISIONEvent(VISON_Model obj)
        {



            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.actionInfo.workLocationId = m_location;
            ActionObj.actionInfo.vehicleId = m_vihicle;
            ActionObj.actionInfo.height = m_Height_Distance_mm.ToString();
            


            if (obj.status == "pickup")//지게차가 물건을 올렸을경우 선반 에서는 물건이 빠질경우
            {
                Tools.Log($"IN##########################pick up Action", Tools.ELogType.BackEndLog);

                Tools.Log($"Wait Sleep 3000 Secound", Tools.ELogType.BackEndLog);
                Thread.Sleep(3000);
                string epc_data = GetMostCountEPC(5,3);
                ActionObj.actionInfo.epc = epc_data;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);


                ActionObj.actionInfo.action = "IN";
                //m_CalRate = CalcLoadRate(obj.area);
                m_CalRate = CalcHeightLoadRate((int)obj.height);
                Tools.Log($"Rate : {obj.area}", Tools.ELogType.BackEndLog);
                Tools.Log($"Copy Before LoadRate  : {m_CalRate}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.loadRate = "0";
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";

                m_LoadMatrix = obj.matrix;
             
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);
                ActionObj.actionInfo.loadMatrix.Add(0);

                ActionObj.actionInfo.loadId = "";
               
                Tools.Log($"Pickup ##QR : {m_qr}", Tools.ELogType.BackEndLog);

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
         
                if (epc_data != "NA")
                {
                    _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
                }
                else
                {
                    Tools.Log("[do not Tray]Pick Up Empty EPC #######", Tools.ELogType.BackEndLog);
                }
                ClearEpc();
            }
            else if(obj.status == "drop")//지게차가 물건을 놨을경우  선반 에서는 물건이 추가될 경우
            {
                Tools.Log($"OUT##########################Drop Action", Tools.ELogType.BackEndLog);
                string epc_data = GetMostCountEPC(8,3);
                ActionObj.actionInfo.epc = epc_data;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.action = "OUT";
                ActionObj.actionInfo.loadRate = m_CalRate.ToString();
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[0]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[1]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[2]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[3]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[4]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[5]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[6]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[7]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[8]);
                ActionObj.actionInfo.loadMatrix.Add(m_LoadMatrix[9]);
                ActionObj.actionInfo.loadId = "";


                Tools.Log($"##QR : {m_qr}", Tools.ELogType.BackEndLog);
                m_qr = "";

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);

                if (epc_data != "NA")
                {
                    Tools.Log($"##LoadRate  : {ActionObj.actionInfo.loadRate}", Tools.ELogType.BackEndLog);
                    _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
                }
                else
                {
                    Tools.Log("[do not Tray]Drop Empty EPC #######", Tools.ELogType.BackEndLog);
                }
                ClearEpc();
                m_CalRate = 0;
                Tools.Log("Clear LoadRate", Tools.ELogType.BackEndLog);
            }
            else
            {
                Tools.Log("Action Idle", Tools.ELogType.BackEndLog);
            }
        }

        public static void PrintDict<K, V>(Dictionary<K, V> dict)
        {
            for (int i = 0; i < dict.Count; i++)
            {
                KeyValuePair<K, V> entry = dict.ElementAt(i);
                Tools.Log("Key: " + entry.Key + ", Value: " + entry.Value, Tools.ELogType.BackEndLog);
            }
        }


        private int CalcHeightLoadRate(int height)
        {
            Tools.Log($"##height  : {height}", Tools.ELogType.BackEndLog);
            float A = (height / (float)180.0);
            float nRet = A * (float)100.0;

            Tools.Log($"##Convert  : {height}", Tools.ELogType.BackEndLog);
            if (nRet <= 0)
            {
                nRet = 0;
            }
            if (nRet >= 97)
            {
                nRet = 97;
            }
            Tools.Log($"##Height Rate  : {nRet}", Tools.ELogType.BackEndLog);
            return (int)nRet;
        }

        private  int  CalcLoadRate(float area)
        {
            Tools.Log($"##area  : {area}", Tools.ELogType.BackEndLog);
            double nRet = (area / 1.62) * 100;

            Tools.Log($"##Convert  : {area}", Tools.ELogType.BackEndLog);


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
