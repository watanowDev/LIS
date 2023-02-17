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

        
  
        public StatusService_GateChecker(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<Gate_Event>().Subscribe(OnGateData, ThreadOption.BackgroundThread, true);
          
            Tools.Log($"StatusService_GateChecker", Tools.ELogType.SystemLog);


            DispatcherTimer StatusClearTimer = new DispatcherTimer();
            StatusClearTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            StatusClearTimer.Tick += new EventHandler(GateCheckTimerEvent);
            StatusClearTimer.Start();




        }

        private void GateCheckTimerEvent(object sender, EventArgs e)
        {


        }


        private static List<QueryRFIDModel> m_Gate_epclist = new List<QueryRFIDModel>();
        

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

            if (m_Gate_epclist.Count > 0)
            {
                DateTime CurrentTime = DateTime.Now;
                //Tools.Log($"Current Time {CurrentTime}  ", Tools.ELogType.BackEndCurrentLog);
           
                int idx = 0;
                while (idx < m_Gate_epclist.Count)
                {
                    TimeSpan Diff = CurrentTime - m_Gate_epclist[idx].Time;
                    int nDiff = Diff.Seconds;


                    if (nDiff > TimeSec)
                    {
                        //Tools.Log($"delete DiffTime {nDiff}  epc {m_location_epclist[idx].EPC} Time {m_location_epclist[idx].Time}  ", Tools.ELogType.BackEndCurrentLog);
                        m_Gate_epclist.Remove(m_Gate_epclist[idx]);

                    }
                    else
                    {
                        ++idx;
                    }
                }

                Dictionary<string, EPC_Value_Model> retRFIDInfoList = new Dictionary<string, EPC_Value_Model>();
                List<int> listCount = new List<int>();
                List<float> listRSSI = new List<float>();


                for (int i = 0; i < m_Gate_epclist.Count; i++)
                {
                    //Tools.Log($"Query  epc {m_location_epclist[i].EPC} RSSI {m_location_epclist[i].RSSI} Time {m_location_epclist[i].Time}  ", Tools.ELogType.BackEndCurrentLog);
                    AddEpcList(m_Gate_epclist[i].EPC, m_Gate_epclist[i].RSSI, ref retRFIDInfoList, ref listCount, ref listRSSI);
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

       
        public void OnGateData(GateRFIDEventModel obj)
        {
            QueryRFIDModel epcModel = new QueryRFIDModel();
            epcModel.EPC = obj.EPC;
            epcModel.Time = DateTime.Now;
            epcModel.RSSI = obj.RSSI;
            m_Gate_epclist.Add(epcModel);
            Tools.Log($"Gate EPC Receive {obj.EPC}", Tools.ELogType.SystemLog);
        }
    }
}
