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
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.VISION;
using Windows.Services.Maps;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.Core.Services
{
    /*
     * StatusService_GateChecker 구성요소
     * RFID : A pluse RFID 수신기 
     */

    public class StatusService_GateChecker : IStatusService
    {
        IEventAggregator _eventAggregator;

        private int gate_in_cnt = 0;
        private int gate_out_cnt = 0;

        private string m_location = "INCHEON_CALT_001";
        private string m_vihicle = "fork_lift003";

        public StatusService_GateChecker(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<Gate_Event>().Subscribe(OnGateData, ThreadOption.BackgroundThread, true);

            Tools.Log($"StatusService_GateChecker", Tools.ELogType.SystemLog);

            DispatcherTimer StatusClearTimer = new DispatcherTimer();
            StatusClearTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            StatusClearTimer.Tick += new EventHandler(GateCheckTimerEvent);
            StatusClearTimer.Start();
        }

        private void GateCheckTimerEvent(object sender, EventArgs e)
        {
            float back_rssi =  (float)0.00; ;
            string back_epc = GetMostBackAntEPC(1, 10, ref back_rssi);
            Tools.Log($"back_epc {back_epc} rssi {back_rssi}", Tools.ELogType.BackEndCurrentLog);


            float front_rssi = (float)0.00; ;
            string front_epc = GetMostFrontAntEPC(1, 10, ref front_rssi);
            Tools.Log($"front_epc {front_epc} rssi{front_rssi} ", Tools.ELogType.BackEndCurrentLog);

            eGateActionType action_type = eGateActionType.UnKnown;
            if (front_rssi > back_rssi)
            {
                Tools.Log($"##Gate OUT##", Tools.ELogType.BackEndCurrentLog);
                gate_out_cnt++;

                //action_type = eGateActionType.IN;
            }
            else if(front_rssi < back_rssi)
            {
                Tools.Log($"##Gate IN##", Tools.ELogType.BackEndCurrentLog);
                gate_in_cnt++;


                //action_type = eGateActionType.OUT;
            }


            if(gate_in_cnt == 3)
            {
                Tools.Log($"##Gate IN##", Tools.ELogType.BackEndLog);
                gate_out_cnt = 0;

                action_type = eGateActionType.IN;
            }


            if (gate_out_cnt == 3)
            {
                Tools.Log($"##Gate OUT##", Tools.ELogType.BackEndLog);
                gate_in_cnt = 0;

                action_type = eGateActionType.OUT;
            }

            if (action_type ==  eGateActionType.IN || action_type == eGateActionType.OUT)
            {

                GateAction(action_type);
            }
        }


        private static List<QueryRFIDModel> m_Gate_FrontAnt_epclist = new List<QueryRFIDModel>();
        private static List<QueryRFIDModel> m_Gate_BackAnt_epclist = new List<QueryRFIDModel>();




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


        private string GetMostBackAntEPC(int TimeSec, int Threshold, ref float rssi)
        {
            string retKeys = "NA";

            if (m_Gate_BackAnt_epclist.Count > 0)
            {
                DateTime CurrentTime = DateTime.Now;
        
                int idx = 0;
                while (idx < m_Gate_BackAnt_epclist.Count)
                {
                    TimeSpan Diff = CurrentTime - m_Gate_BackAnt_epclist[idx].Time;
                    int nDiff = Diff.Seconds;


                    if (nDiff > TimeSec)
                    {
                        m_Gate_BackAnt_epclist.Remove(m_Gate_BackAnt_epclist[idx]);

                    }
                    else
                    {
                        ++idx;
                    }
                }

                Dictionary<string, EPC_Value_Model> retRFIDInfoList = new Dictionary<string, EPC_Value_Model>();
                List<int> listCount = new List<int>();
                List<float> listRSSI = new List<float>();


                for (int i = 0; i < m_Gate_BackAnt_epclist.Count; i++)
                {
                    AddEpcList(m_Gate_BackAnt_epclist[i].EPC, m_Gate_BackAnt_epclist[i].RSSI, ref retRFIDInfoList, ref listCount, ref listRSSI);
                }

                RSSI_AverageEPCList(ref retRFIDInfoList, Tools.ELogType.BackEndCurrentLog);

                if (retRFIDInfoList.Count > 0)
                {
                    retKeys = retRFIDInfoList.Aggregate((x, y) => x.Value.RSSI > y.Value.RSSI ? x : y).Key;

                    if (retRFIDInfoList.TryGetValue(retKeys, out EPC_Value_Model Temp))
                    {
                        rssi = Temp.RSSI;

                        Tools.Log($"EPCKey Count {Temp.EPC_Check_Count}", Tools.ELogType.BackEndCurrentLog);
                        Tools.Log($"EPCKey RSSI {Temp.RSSI}", Tools.ELogType.BackEndCurrentLog);
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


        private string GetMostFrontAntEPC(int TimeSec, int Threshold,  ref float rssi)
        {
            string retKeys = "NA";

            if (m_Gate_FrontAnt_epclist.Count > 0)
            {
                DateTime CurrentTime = DateTime.Now;
            
                int idx = 0;
                while (idx < m_Gate_FrontAnt_epclist.Count)
                {
                    TimeSpan Diff = CurrentTime - m_Gate_FrontAnt_epclist[idx].Time;
                    int nDiff = Diff.Seconds;


                    if (nDiff > TimeSec)
                    {
                        m_Gate_FrontAnt_epclist.Remove(m_Gate_FrontAnt_epclist[idx]);

                    }
                    else
                    {
                        ++idx;
                    }
                }

                Dictionary<string, EPC_Value_Model> retRFIDInfoList = new Dictionary<string, EPC_Value_Model>();
                List<int> listCount = new List<int>();
                List<float> listRSSI = new List<float>();


                for (int i = 0; i < m_Gate_FrontAnt_epclist.Count; i++)
                {
                    AddEpcList(m_Gate_FrontAnt_epclist[i].EPC, m_Gate_FrontAnt_epclist[i].RSSI, ref retRFIDInfoList, ref listCount, ref listRSSI);
                }

                RSSI_AverageEPCList(ref retRFIDInfoList , Tools.ELogType.BackEndCurrentLog);

                if (retRFIDInfoList.Count > 0)
                {
                    retKeys = retRFIDInfoList.Aggregate((x, y) => x.Value.RSSI > y.Value.RSSI ? x : y).Key;

                    if (retRFIDInfoList.TryGetValue(retKeys, out EPC_Value_Model Temp))
                    {
                        rssi = Temp.RSSI;
                        Tools.Log($"EPCKey Count {Temp.EPC_Check_Count}", Tools.ELogType.BackEndCurrentLog);
                        Tools.Log($"EPCKey RSSI {Temp.RSSI}", Tools.ELogType.BackEndCurrentLog);
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

       
        public void OnGateData(GateRFIDEventModel obj)
        {
            QueryRFIDModel epcModel = new QueryRFIDModel();

            if (obj.GateValue == "0")
            {
                epcModel.EPC = obj.EPC;
                epcModel.Time = DateTime.Now;
                epcModel.RSSI = obj.RSSI;
                m_Gate_FrontAnt_epclist.Add(epcModel);
            //  Tools.Log($"front gate {epcModel.EPC} ", Tools.ELogType.BackEndLog);
            }
            else
            {
                epcModel.EPC = obj.EPC;
                epcModel.Time = DateTime.Now;
                epcModel.RSSI = obj.RSSI;
                m_Gate_BackAnt_epclist.Add(epcModel);
                //Tools.Log($"back gate {epcModel.EPC} ", Tools.ELogType.BackEndLog);
            }
            //Tools.Log($"Gate EPC Receive {obj.EPC}", Tools.ELogType.SystemLog);
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
            post_obj.url = "https://192.168.0.20/monitoring/geofence/addition-info/logistics/heavy-equipment/gate-event";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }
    }
}
