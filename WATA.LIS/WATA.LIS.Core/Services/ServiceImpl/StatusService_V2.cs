using MaterialDesignThemes.Wpf;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.VISION;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.Core.Services
{
    /*
     * StatusService_V2 구성요소
     * 거리센서 : TeraBee EVO-60 (높이 측정)
     * RFID : A pluse RFID 수신기 
     * VISION : Astra-S, Astra-Plus
     */

    public class StatusService_V2 : IStatusService
    {
        IEventAggregator _eventAggregator;

        public int      m_Height_Distance_mm { get; set; }
 
        private int rifid_status_check_count = 0;
        private int distance_status_check_count = 35;
        private int status_limit_count = 3;

        private string m_location = "INCHEON_CALT_001";
        private string m_vihicle = "fork_lift001";
        private string m_errorcode = "0000";

        private bool m_stop_rack_epc = true;


        public StatusService_V2(IEventAggregator eventAggregator , IMainModel main)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RackProcess_Event>().Subscribe(OnRFIDLackData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<LocationProcess_Event>().Subscribe(OnLocationData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<VISION_Event>().Subscribe(OnVISIONEvent, ThreadOption.BackgroundThread, true);

            DispatcherTimer StatusClearTimer = new DispatcherTimer();
            StatusClearTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            StatusClearTimer.Tick += new EventHandler(StatusClearEvent);
            StatusClearTimer.Start();

            DispatcherTimer CurrentTimer = new DispatcherTimer();
            CurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            CurrentTimer.Tick += new EventHandler(CurrentLocationTimerEvent);
            CurrentTimer.Start();
            MainConfigModel mainobj = (MainConfigModel)main;
            m_vihicle = mainobj.forkLiftID;
            Tools.Log($"Start Status Service", Tools.ELogType.SystemLog);
        }

        //private string m_Location_epc = "";


        private void CurrentLocationTimerEvent(object sender, EventArgs e)
        {
            string epc = GetMostlocationEPC(1, 0);

            LocationInfoModel location_obj = new LocationInfoModel();

            location_obj.locationInfo.vehicleId = m_vihicle;
            location_obj.locationInfo.workLocationId = m_location;
            location_obj.locationInfo.epc = epc;

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

        bool Is_front_ant_disable = false;

        private void StatusClearEvent(object sender, EventArgs e)
        {
            if(rifid_status_check_count >  status_limit_count)
            {
                RackRFIDEventModel rfidmodel = new RackRFIDEventModel();
                ClearEpc();
                Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
                rfidmodel.EPC = "field";
                _eventAggregator.GetEvent<RackProcess_Event>().Publish(rfidmodel);

                Is_front_ant_disable = true;

            }
            else
            {
                rifid_status_check_count++;
                Tools.Log($"Wait Count {rifid_status_check_count}", Tools.ELogType.SystemLog);

                Is_front_ant_disable = false;
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

        private static List<QueryRFIDModel> m_rack_epclist = new List<QueryRFIDModel>();

        private static List<QueryRFIDModel> m_location_epclist = new List<QueryRFIDModel>();


        private void AddEpcList(string key_epc,
                                float value_rssi,
                                ref Dictionary<string, EPC_Value_Model> retRFIDInfoList, 
                                ref List<int> listCount,
                                ref List<float> listRSSI)
        {
            if (retRFIDInfoList.ContainsKey(key_epc))
            {
                int idx = Array.IndexOf(retRFIDInfoList.Keys.ToArray(), key_epc);
                listCount[idx] ++;
                listRSSI[idx] += value_rssi;
                retRFIDInfoList[key_epc].EPC_Check_Count = listCount[idx];
                retRFIDInfoList[key_epc].RSSI = listRSSI[idx];
            }
            else//Dictionary first data
            {
                EPC_Value_Model temp = new EPC_Value_Model();
                temp.EPC_Check_Count = 1;
                temp.RSSI = value_rssi;
                retRFIDInfoList.Add(key_epc, temp);
                listCount.Add(1);
                listRSSI.Add(value_rssi);
            }
        }

        private void RSSI_AverageEPCList(ref Dictionary<string, EPC_Value_Model> retRFIDInfoList , ELogType logtype )
        {
            foreach (KeyValuePair<string, EPC_Value_Model> item  in retRFIDInfoList)
            {
               
                //Tools.Log($"Before RSSI : {item.Value.RSSI} Count {item.Value.EPC_Check_Count}", logtype);
                float avg = item.Value.RSSI / item.Value.EPC_Check_Count;
                //Tools.Log($"After RSSI Average :  {avg}", logtype);
                item.Value.RSSI = avg;
                //Tools.Log($"EPC [{item.Key}] RSSI [{item.Value.RSSI}] Count [{item.Value.EPC_Check_Count}]", logtype);
            }
        }

        
        
     

        private string GetMostlocationEPC(int TimeSec, int Threshold)
        {
            string retKeys = "NA";

            if (m_location_epclist.Count > 0)
            {
                DateTime CurrentTime = DateTime.Now;
                //Tools.Log($"Current Time {CurrentTime}  ", Tools.ELogType.BackEndCurrentLog);
           
                int idx = 0;
                while (idx < m_location_epclist.Count)
                {
                    TimeSpan Diff = CurrentTime - m_location_epclist[idx].Time;
                    int nDiff = Diff.Seconds;


                    if (nDiff > TimeSec)
                    {
                        //Tools.Log($"delete DiffTime {nDiff}  epc {m_location_epclist[idx].EPC} Time {m_location_epclist[idx].Time}  ", Tools.ELogType.BackEndCurrentLog);
                        m_location_epclist.Remove(m_location_epclist[idx]);

                    }
                    else
                    {
                        ++idx;
                    }
                }

                Dictionary<string, EPC_Value_Model> retRFIDInfoList = new Dictionary<string, EPC_Value_Model>();
                List<int> listCount = new List<int>();
                List<float> listRSSI = new List<float>();


                for (int i = 0; i < m_location_epclist.Count; i++)
                {
                    //Tools.Log($"Query  epc {m_location_epclist[i].EPC} RSSI {m_location_epclist[i].RSSI} Time {m_location_epclist[i].Time}  ", Tools.ELogType.BackEndCurrentLog);
                    AddEpcList(m_location_epclist[i].EPC, m_location_epclist[i].RSSI, ref retRFIDInfoList, ref listCount, ref listRSSI);
                }

                RSSI_AverageEPCList(ref retRFIDInfoList , Tools.ELogType.BackEndCurrentLog);

                if (retRFIDInfoList.Count > 0)
                {
                    //PrintDict(retRFIDInfoList);
                    retKeys = retRFIDInfoList.Aggregate((x, y) => x.Value.RSSI > y.Value.RSSI ? x : y).Key;

                    if (retRFIDInfoList.TryGetValue(retKeys, out EPC_Value_Model Temp))
                    {
                        Tools.Log($"EPCKey Count {Temp.EPC_Check_Count}", Tools.ELogType.BackEndCurrentLog);
                        Tools.Log($"EPCKey RSSI {Temp.RSSI}", Tools.ELogType.BackEndCurrentLog);
                        //if (Temp.EPC_Check_Count < Threshold)
                        //{
                        //    retKeys = "NA";
                        //    Tools.Log($"Low Count {Temp.EPC_Check_Count}", Tools.ELogType.BackEndLog);
                        //}
                    }
                }
                else
                {
                    Tools.Log("Dic List Empty", Tools.ELogType.BackEndCurrentLog);
                }
            }
            else
            {
                Tools.Log("EPC List Empty", Tools.ELogType.BackEndCurrentLog);
            }
            return retKeys;
        }

        private string GetMostRackEPC(int TimeSec,float Threshold ,ref float rssi, int H_distance)
        {
            string retKeys = "field";

            Tools.Log($"Time {TimeSec} Threshold {Threshold}", Tools.ELogType.BackEndLog);



            if (m_rack_epclist.Count > 0)
            {
                DateTime CurrentTime = DateTime.Now;
                Tools.Log($"Current Time {CurrentTime}  ", Tools.ELogType.BackEndLog);

                int idx = 0;
                while (idx < m_rack_epclist.Count)
                {
                    TimeSpan Diff = CurrentTime - m_rack_epclist[idx].Time;
                    int nDiff = Diff.Seconds;


                    if (nDiff > TimeSec)
                    {
                        Tools.Log($"delete DiffTime {nDiff}  epc {m_rack_epclist[idx].EPC} Time {m_rack_epclist[idx].Time}  ", Tools.ELogType.BackEndLog);
                        m_rack_epclist.Remove(m_rack_epclist[idx]);

                    }
                    else
                    {
                        ++idx;
                    }
                }


                Dictionary<string, EPC_Value_Model> retRFIDInfoList = new Dictionary<string, EPC_Value_Model>();
                List<int>   listCount   = new List<int>();
                List<float> listRSSI    = new List<float>();


                for (int i = 0; i < m_rack_epclist.Count; i++)
                {
                    Tools.Log($"Query  epc {m_rack_epclist[i].EPC} RSSI {m_rack_epclist[i].RSSI} Time {m_rack_epclist[i].Time}  ", Tools.ELogType.BackEndLog);
                    AddEpcList(m_rack_epclist[i].EPC, m_rack_epclist[i].RSSI , ref retRFIDInfoList, ref listCount, ref listRSSI);
                }

                RSSI_AverageEPCList(ref retRFIDInfoList , Tools.ELogType.BackEndLog);


                if (retRFIDInfoList.Count > 0)
                {
                    retKeys = retRFIDInfoList.Aggregate((x, y) => x.Value.RSSI > y.Value.RSSI ? x : y).Key;

                    if (retRFIDInfoList.TryGetValue(retKeys, out EPC_Value_Model Temp))
                    {
                        Tools.Log($"EPCKey Count {Temp.EPC_Check_Count}", Tools.ELogType.BackEndLog);
                        Tools.Log($"EPCKey RSSI {Temp.RSSI}", Tools.ELogType.BackEndLog);

                        rssi = Temp.RSSI;
                        if (rssi < Threshold)
                        {
                            if (H_distance > 1500)
                            {
                                retKeys = "field";
                                Tools.Log("##filed## ##Event##", Tools.ELogType.BackEndLog);
                            }
                            else
                            {
                                Tools.Log("##High Floor rack## ##Event##", Tools.ELogType.BackEndLog);
                            }
                        }
                        else
                        {
                            Tools.Log("##rack## ##Event##", Tools.ELogType.BackEndLog);
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
            m_rack_epclist.Clear();
            Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
        }

       
        public void OnLocationData(LocationRFIDEventModel obj)
        {
            if(Is_front_ant_disable)
            {
                QueryRFIDModel epcModel = new QueryRFIDModel();
                epcModel.EPC = obj.EPC;
                epcModel.Time = DateTime.Now;
                epcModel.RSSI = obj.RSSI;
                m_location_epclist.Add(epcModel);
                Tools.Log($"Location EPC Receive {obj.EPC}", Tools.ELogType.SystemLog);
            }
        }

        public void OnRFIDLackData(RackRFIDEventModel obj)
        {
            rifid_status_check_count = 0;//erase status clear

            QueryRFIDModel epcModel = new QueryRFIDModel();
            epcModel.EPC = obj.EPC;
            epcModel.Time = DateTime.Now;
            epcModel.RSSI = obj.RSSI;

            if (m_stop_rack_epc)
            {
    
                m_rack_epclist.Add(epcModel);

                if (m_rack_epclist.Count >= 50)
                {
                    m_rack_epclist.RemoveAt(0);
                }
                Tools.Log($"Lack EPC Recieve :  {obj.EPC}", Tools.ELogType.SystemLog);
            }
            else
            {
                Tools.Log($"Stop Rack EPC", Tools.ELogType.SystemLog);
            }

            m_location_epclist.Add(epcModel);
            Tools.Log($"Location EPC Receive {obj.EPC}", Tools.ELogType.SystemLog);
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
                Tools.Log($"Wait Sleep 2000 Secound", Tools.ELogType.BackEndLog);
                Thread.Sleep(2000);
                Tools.Log($"Stop receive rack epc", Tools.ELogType.BackEndLog);
                m_stop_rack_epc = false;
                float rssi = (float)0.00;

                string epc_data = GetMostRackEPC(3,-72, ref rssi, m_Height_Distance_mm);
                ActionObj.actionInfo.epc = epc_data;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.action = "IN";
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

                m_qr = ActionObj.actionInfo.containerId = "";// obj.qr;
                ActionObj.actionInfo.loadId = "";
               
                Tools.Log($"Pickup ##QR : {m_qr}", Tools.ELogType.BackEndLog);

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
         
                if (epc_data != "field")
                {
                    _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
                }
                else
                {
                    Tools.Log("[do not Tray]Pick Up Empty EPC #######", Tools.ELogType.BackEndLog);
                }
                ClearEpc();
                m_stop_rack_epc = true;
                Tools.Log("start receive rack epc", Tools.ELogType.BackEndLog);
                Tools.Log($"Action : [pickup] EPC : [{epc_data}] Rssi : [{rssi}] ", Tools.ELogType.ActionLog);
            }
            else if(obj.status == "drop")//지게차가 물건을 놨을경우  선반 에서는 물건이 추가될 경우
            {
                float rssi = (float)0.00;

                Tools.Log($"OUT##########################Drop Action", Tools.ELogType.BackEndLog);


                Tools.Log($"Stop receive rack epc 33", Tools.ELogType.BackEndLog);
                m_stop_rack_epc = false;

                string epc_data = GetMostRackEPC(10, -68, ref rssi, m_Height_Distance_mm);
                ActionObj.actionInfo.epc = epc_data;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.action = "OUT";
                ActionObj.actionInfo.loadRate = m_CalRate.ToString();
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";
                if (m_LoadMatrix != null)
                {
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
                }
                ActionObj.actionInfo.containerId = "";//m_qr;
                ActionObj.actionInfo.loadId = "";

                Tools.Log($"##QR : {m_qr}", Tools.ELogType.BackEndLog);
                m_qr = "";

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);

                if (epc_data != "field")
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
                m_stop_rack_epc = true;
                Tools.Log("start receive rack epc", Tools.ELogType.BackEndLog);


                Tools.Log($"Action : [drop] EPC : [{epc_data}] Rssi : [{rssi}] ", Tools.ELogType.ActionLog);
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
