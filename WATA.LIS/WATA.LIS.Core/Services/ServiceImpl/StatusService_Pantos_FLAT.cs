using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Layout;
using Newtonsoft.Json.Linq;
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
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.StatusLED;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Events.NAVSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.ErrorCheck;
using WATA.LIS.Core.Model.Indicator;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.VISION;
using WATA.LIS.Core.Model.NAV;
using Windows.Security.Cryptography.Core;
using static WATA.LIS.Core.Common.Tools;
using System.IO;
using System.Net;
using System.Security.Policy;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace WATA.LIS.Core.Services
{
    /*
     * StatusService_Pantos 구성요소
     * 거리센서 : TeraBee EVO-15 (높이 측정)
     * RFID : A pluse RFID 수신기 
     * VISION : Astra-FemtoW
     */

    public class StatusService_Pantos_FLAT : IStatusService
    {
        IEventAggregator _eventAggregator;

        public int m_Height_Distance_mm { get; set; }

        private int rifid_status_check_count = 0;
        private int distance_status_check_count = 35;
        private int status_limit_count = 10;

        private string m_location = "PANTOS_FLAT_001";
        private string m_vihicle = "fork_lift004";
        private string m_errorcode = "0000";

        private bool m_stop_rack_epc = true;
        private RFIDConfigModel rfidConfig;

        private VisionConfigModel visionConfig;

        private WeightSensorModel m_weight;
        private List<WeightSensorModel> m_weight_list;
        private const int sample_size = 50;
        WeightConfigModel _weightConfig;
        DistanceConfigModel _distance;

        public string mapId { get; set; }
        public string mappingId { get; set; }
        public string projectId { get; set; }

        public long naviX { get; set; }
        public long naviY { get; set; }
        public long naviT { get; set; }
        public string zoneId { get; set; }
        public string zoneName { get; set; }
        CellInfoModel cellInfoModel;

        private readonly IWeightModel _weightmodel;
        DispatcherTimer BuzzerTimer;

        public StatusService_Pantos_FLAT(IEventAggregator eventAggregator, IMainModel main, IRFIDModel rfidmodel, IVisionModel visionModel, IWeightModel weightmodel, IDistanceModel distanceModel)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);
            //_eventAggregator.GetEvent<RackProcess_Event>().Subscribe(OnRFIDLackData, ThreadOption.BackgroundThread, true);
            //_eventAggregator.GetEvent<LocationProcess_Event>().Subscribe(OnLocationData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<VISION_Event>().Subscribe(OnVISIONEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<WeightSensorEvent>().Subscribe(OnWeightSensor, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<BackEndReturnCodeEvent>().Subscribe(OnContainerReturn, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<IndicatorRecvEvent>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);

            _eventAggregator.GetEvent<NAVSensorEvent>().Subscribe(OnNAVSensorEvent, ThreadOption.BackgroundThread, true);


            _weightmodel = weightmodel;
            _distance = (DistanceConfigModel)distanceModel;

            _weightConfig = (WeightConfigModel)_weightmodel;


            DispatcherTimer StatusClearTimer = new DispatcherTimer();
            StatusClearTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            StatusClearTimer.Tick += new EventHandler(StatusClearEvent);
            StatusClearTimer.Start();

            DispatcherTimer CurrentTimer = new DispatcherTimer();
            CurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            CurrentTimer.Tick += new EventHandler(CurrentLocationTimerEvent);
            CurrentTimer.Start();

            MainConfigModel mainobj = (MainConfigModel)main;
            m_vihicle = mainobj.vehicleId;
            visionConfig = (VisionConfigModel)visionModel;
            rfidConfig = (RFIDConfigModel)rfidmodel;
            mapId = mainobj.mapId;
            mappingId = mainobj.mappingId;
            projectId = mainobj.projectId;

            DispatcherTimer ErrorCheckTimer = new DispatcherTimer();
            ErrorCheckTimer.Interval = new TimeSpan(0, 0, 0, 0, 2000);
            ErrorCheckTimer.Tick += new EventHandler(StatusErrorCheckEvent);
            ErrorCheckTimer.Start();



            DispatcherTimer AliveTimer = new DispatcherTimer();
            AliveTimer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            AliveTimer.Tick += new EventHandler(AliveTimerEvent);
            AliveTimer.Start();



            BuzzerTimer = new DispatcherTimer();
            BuzzerTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            BuzzerTimer.Tick += new EventHandler(BuzzerTimerEvent);


            DispatcherTimer IndicatorTimer = new DispatcherTimer();
            IndicatorTimer.Interval = new TimeSpan(0, 0, 0, 0, 300);
            IndicatorTimer.Tick += new EventHandler(IndicatorSendTimerEvent);
            IndicatorTimer.Start();



            m_weight = new WeightSensorModel();
            m_weight_list = new List<WeightSensorModel>();

            GetCellListFromPlatform();

        }

        private string m_Location_epc = "";

        public void OnWeightSensor(WeightSensorModel obj)
        {
            m_weight_list.Add(obj);

            if (m_weight_list.Count >= sample_size)
            {
                m_weight.LeftWeight = GetStableValue(m_weight_list.Select(w => w.LeftWeight).ToList());
                m_weight.RightWeight = GetStableValue(m_weight_list.Select(w => w.RightWeight).ToList());
                m_weight.GrossWeight = GetStableValue(m_weight_list.Select(w => w.GrossWeight).ToList());
                Tools.Log($"Weight {m_weight}", Tools.ELogType.SystemLog);

                m_weight_list.RemoveAt(0);
            }
            else
            {
                m_weight.LeftWeight = obj.LeftWeight;
                m_weight.RightWeight = obj.RightWeight;
                m_weight.GrossWeight = obj.GrossWeight;
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
                Tools.Log($"Weight Sensor Read Error!!!", Tools.ELogType.SystemLog);
            }
            return ret;
        }

        public void OnContainerReturn(int status)
        {
            if (status != 200)
            {
                Pattlite_Buzzer_LED(ePlayBuzzerLed.EMERGENCY2);
                Tools.Log($"IN########################## EMERGENCY2", Tools.ELogType.BackEndLog);
            }
        }




        public void OnIndicatorEvent(string status)
        {
            Tools.Log($"OnIndicatorEvent1111 {status}", Tools.ELogType.DisplayLog);

            if (status == "pick_up")
            {
                Pattlite_Buzzer_LED(ePlayBuzzerLed.MEASRUE_OK);
            }
        }

        private void CurrentLocationTimerEvent(object sender, EventArgs e)
        {
            // 위치 전송
        }

        private void IndicatorSendTimerEvent(object sender, EventArgs e)
        {
            SendToIndicator(m_weight.GrossWeight, m_weight.LeftWeight, m_weight.RightWeight, m_qr, m_vision_width, m_vision_height, m_vision_depth);

            //SendToIndicator(test1 ++, test2 ++, test3 ++, test_qr.ToString(), test4 ++ , test5 ++);
            //test_qr++;
        }

        private void BuzzerTimerEvent(object sender, EventArgs e)
        {
            Tools.Log("BuzzerTimerEvent", Tools.ELogType.BackEndLog);
            Pattlite_Buzzer_LED(ePlayBuzzerLed.EMERGENCY);
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
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndCurrent;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/alive";

            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

        }

        bool Is_front_ant_disable = false;



        private void StatusErrorCheckEvent(object sender, EventArgs e)
        {
            do
            {
                if (GlobalValue.IS_ERROR.camera == false)
                {
                    Tools.Log("Camera Error", Tools.ELogType.SystemLog);
                }


                if (GlobalValue.IS_ERROR.backend == false)
                {
                    Tools.Log("BackEnd Error", Tools.ELogType.SystemLog);
                }


                if (GlobalValue.IS_ERROR.rfid == false)
                {
                    Tools.Log("rifid Error", Tools.ELogType.SystemLog);
                }



                if (GlobalValue.IS_ERROR.distance == false)
                {
                    Tools.Log("distance Error", Tools.ELogType.SystemLog);
                }



                if (GlobalValue.IS_ERROR.distance == false ||
                   GlobalValue.IS_ERROR.backend == false ||
                   GlobalValue.IS_ERROR.rfid == false ||
                   GlobalValue.IS_ERROR.distance == false)
                {

                    _eventAggregator.GetEvent<StatusLED_Event>().Publish("red");



                    break;
                }


                _eventAggregator.GetEvent<StatusLED_Event>().Publish("green");


            }
            while (false);

        }

        private void StatusClearEvent(object sender, EventArgs e)
        {

            if (rifid_status_check_count > status_limit_count)
            {
                RackRFIDEventModel rfidmodel = new RackRFIDEventModel();
                ClearEpc();
                //    Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
                rfidmodel.EPC = "field";
                rfidmodel.RSSI = -99;
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
                GlobalValue.IS_ERROR.distance = false;

                DistanceSensorModel DisTanceObject = new DistanceSensorModel();
                m_Height_Distance_mm = -100;
                DisTanceObject.Distance_mm = m_Height_Distance_mm;
                _eventAggregator.GetEvent<DistanceSensorEvent>().Publish(DisTanceObject);
                Tools.Log("#######Distance Status Clear #######", Tools.ELogType.SystemLog);
            }
            else
            {
                GlobalValue.IS_ERROR.distance = true;
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
                listCount[idx]++;
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

        private void RSSI_AverageEPCList(ref Dictionary<string, EPC_Value_Model> retRFIDInfoList, ELogType logtype)
        {
            foreach (KeyValuePair<string, EPC_Value_Model> item in retRFIDInfoList)
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

                RSSI_AverageEPCList(ref retRFIDInfoList, Tools.ELogType.BackEndCurrentLog);

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

        private string GetMostRackEPC(ref bool shelf, int TimeSec, float Threshold, ref float rssi, int H_distance)
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
                List<int> listCount = new List<int>();
                List<float> listRSSI = new List<float>();


                for (int i = 0; i < m_rack_epclist.Count; i++)
                {
                    Tools.Log($"Query  epc {m_rack_epclist[i].EPC} RSSI {m_rack_epclist[i].RSSI} Time {m_rack_epclist[i].Time}  ", Tools.ELogType.BackEndLog);
                    AddEpcList(m_rack_epclist[i].EPC, m_rack_epclist[i].RSSI, ref retRFIDInfoList, ref listCount, ref listRSSI);
                }

                RSSI_AverageEPCList(ref retRFIDInfoList, Tools.ELogType.BackEndLog);

                shelf = true;


                if (retRFIDInfoList.Count > 0)
                {
                    retKeys = retRFIDInfoList.Aggregate((x, y) => x.Value.RSSI > y.Value.RSSI ? x : y).Key;

                    if (retRFIDInfoList.TryGetValue(retKeys, out EPC_Value_Model Temp))
                    {
                        Tools.Log($"EPCKey Count {Temp.EPC_Check_Count}", Tools.ELogType.BackEndLog);
                        Tools.Log($"EPCKey RSSI {Temp.RSSI}", Tools.ELogType.BackEndLog);

                        if (rssi < Threshold)
                        {

                            if (H_distance < 900)
                            {
                                //retKeys = "field";
                                shelf = false;
                                //Tools.Log("##filed## ##Event##", Tools.ELogType.BackEndLog);
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

                        if (retKeys == "field")
                        {
                            Tools.Log("##field## ##Event##", Tools.ELogType.BackEndLog);
                            shelf = false;
                        }
                    }
                }
                else
                {
                    shelf = false;
                    Tools.Log("Dic List Empty", Tools.ELogType.BackEndLog);
                }
            }
            else
            {
                shelf = false;
                Tools.Log("EPC List Empty", Tools.ELogType.BackEndLog);
            }

            return retKeys;
        }

        private void ClearEpc()
        {
            m_rack_epclist.Clear();
            //Tools.Log("Clear EPC", Tools.ELogType.BackEndLog);
        }

        private int WaitLoadSensor(int distanceThreshold)
        {
            int count = 0;
            int nRet = -1;

            while (true)
            {
                if (count > 100)
                {
                    nRet = -1;
                    break;
                }

                if (m_weight.GrossWeight > 10)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.ACTION_START);

                    Thread.Sleep(1000);


                    //Thread.Sleep(_weightConfig.loadweight_timeout);

                    nRet = 0;
                    break;
                }


                Thread.Sleep(100);
                count++;

            }
            return nRet;
        }



        /*
        public void OnLocationData(LocationRFIDEventModel obj)
        {
            if (Is_front_ant_disable)
            {
                QueryRFIDModel epcModel = new QueryRFIDModel();
                epcModel.EPC = obj.EPC;
                //  epcModel.EPC = "DA00026300010000000100ED";
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
            //epcModel.EPC = "DA00026300010000000100ED";


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
        */


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
            float A = (height / (float)090.0);
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

        private int CalcLoadRate(float area)
        {
            Tools.Log($"##area  : {area}", Tools.ELogType.BackEndLog);
            double nRet = (area / 1.62) * 100;

            Tools.Log($"##Convert  : {area}", Tools.ELogType.BackEndLog);


            if (nRet <= 0)
            {
                nRet = 0;
            }
            if (nRet >= 97)
            {
                nRet = 97;
            }
            Tools.Log($"##Rate  : {nRet}", Tools.ELogType.BackEndLog);


            return (int)nRet;
        }

        private void Pattlite_Buzzer_LED(ePlayBuzzerLed value)
        {
            if (value == ePlayBuzzerLed.ACTION_FAIL)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.ACTION_START)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Blue;
                model.BuzzerPattern = eBuzzerPatterns.Pattern1;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.ACTION_FINISH)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern1;
                model.LED_Color = eLEDColors.Purple;
                model.BuzzerPattern = eBuzzerPatterns.Pattern4;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.DROP)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Continuous;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
            else if (value == ePlayBuzzerLed.EMERGENCY)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Pattern1;
                model.BuzzerCount = 0;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.EMERGENCY2)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Continuous;
                model.LED_Color = eLEDColors.Red;
                model.BuzzerPattern = eBuzzerPatterns.Pattern2;
                model.BuzzerCount = 0;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }

            else if (value == ePlayBuzzerLed.MEASRUE_OK)
            {
                Pattlite_LED_Buzzer_Model model = new Pattlite_LED_Buzzer_Model();
                model.LED_Pattern = eLEDPatterns.Pattern1;
                model.LED_Color = eLEDColors.Green;
                model.BuzzerPattern = eBuzzerPatterns.Pattern1;
                model.BuzzerCount = 1;
                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Publish(model);
            }
        }

        private void SendToIndicator(int grossWeight, int leftweight, int rightweight, string QR, float vision_w, float vision_h, float vsion_depth)
        {
            IndicatorModel Model = new IndicatorModel();
            Model.forklift_status.weightTotal = grossWeight;
            Model.forklift_status.weightLeft = leftweight;
            Model.forklift_status.weightRight = rightweight;
            Model.forklift_status.QR = QR;
            Model.forklift_status.visionWidth = vision_w;
            Model.forklift_status.visionHeight = vision_h;
            Model.forklift_status.visionDepth = vsion_depth;
            string json_body = Util.ObjectToJson(Model);
            _eventAggregator.GetEvent<IndicatorSendEvent>().Publish(json_body);
        }

        /// <summary>
        ///  임시 ///////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        public void GetCellListFromPlatform()
        {
            try
            {
                string param = "mapId=" + mapId + "&mappingId=" + mappingId + "&projectId=" + projectId;
                string url = "https://dev-lms-api.watalbs.com/monitoring/plane/plane-poc/plane-groups?" + param;
                Tools.Log($"REST Get Client url: {url}", Tools.ELogType.BackEndLog);

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
                            cellInfoModel = JsonConvert.DeserializeObject<CellInfoModel>(sr.ReadToEnd());
                            for (int i = 0; i < cellInfoModel.data.Count; i++)
                            {
                                if (cellInfoModel.data[i].targetGeofence.Count > 0)
                                {
                                    for (int j = 0; j < cellInfoModel.data[i].targetGeofence.Count; j++)
                                    {
                                        string pattern = @"POINT\((\d+\.\d+) (\d+\.\d+)\)";
                                        Match match = Regex.Match(cellInfoModel.data[i].targetGeofence[j].geom, pattern);
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
                    
                        //    CalcDistanceAndGetZoneID(14096609073, 4510553755, false);
                        //Tools.Log($"REST GetCellListFromPlatform status : {status}, {Util.ObjectToJson(cellInfoModel)} ", Tools.ELogType.BackEndLog);
                        /* TEST
                        //Tools.Log($"REST GetCellListFromPlatform status : {status}, {Util.ObjectToJson(cellInfoModel)} ", Tools.ELogType.BackEndLog);
                        CalcDistanceAndGetZoneID(14096615041, 4510558400);
                    
                        

                        VISON_Model visionModel = new VISON_Model();
                        visionModel.area = 200;
                        visionModel.width = 105;
                        visionModel.height = 0;
                        visionModel.qr = "08e8396ac20d5230a9202c7592754154";
                        visionModel.has_roof = true ;
                        visionModel.depth = 1;
                        visionModel.status = "1";
                        visionModel.matrix = new byte[10] { 0,1,2,3,4,5,6,7,8,9 };
                        visionModel.status = "drop";
                        
                        _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel);
                        */
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"REST Get Client Response Error: {ex}", Tools.ELogType.BackEndLog);
            }
        }

        public void CalcDistanceAndGetZoneID(long naviX, long naviY, bool bDrop)
        {
            long distance = 300;
            zoneId = "";
            zoneName = "";
            if (cellInfoModel != null && cellInfoModel.data.Count > 0)
            {
                for (int i = 0; i < cellInfoModel.data.Count; i++)
                {
                    if (cellInfoModel.data[i].targetGeofence.Count > 0)
                    {
                        for (int j = cellInfoModel.data[i].targetGeofence.Count - 1; j >= 0; j--)
                        {
                            string pattern = @"POINT\((\d+\.\d+) (\d+\.\d+)\)";
                            Match match = Regex.Match(cellInfoModel.data[i].targetGeofence[j].geom, pattern);
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
                                        zoneId = cellInfoModel.data[i].targetGeofence[j + 1].zoneId;
                                        zoneName = cellInfoModel.data[i].targetGeofence[j + 1].zoneName;
                                    }
                                    else
                                    {
                                        zoneId = cellInfoModel.data[i].targetGeofence[j].zoneId;
                                        zoneName = cellInfoModel.data[i].targetGeofence[j].zoneName;
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
                Tools.Log($"[ERROR] Can't get cellInfoModel ", Tools.ELogType.BackEndLog);
            }

        }

        public void OnNAVSensorEvent(NAVSensorModel navSensorModel)
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

            naviX = navSensorModel.naviX;
            naviY = navSensorModel.naviY;

            navSensorModel.zoneId = zoneId;
            navSensorModel.zoneName = zoneName;
            navSensorModel.mapId = mapId;
            navSensorModel.mappingId = mappingId;
            navSensorModel.projectId = projectId;
            navSensorModel.vehicleId = m_vihicle;

            //CalcDistanceAndGetZoneID(naviX, naviY, false);

            string json_body = Util.ObjectToJson(navSensorModel);
            json_body = "{ \"navigation\":" + json_body + "}";
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/plane/plane-poc/heavy-equipment/location";

            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

            Tools.Log($"{json_body}", Tools.ELogType.BackEndLog);

        }


        private int m_CalRate = 0;

        private byte[] m_LoadMatrix = new byte[10];
        private float m_vision_width = 0;
        private float m_vision_height = 0;
        private float m_vision_depth = 0;

        private string m_qr = "";
        private string m_container_qr = "";
        public void OnVISIONEvent(VISON_Model obj)
        {

            if (obj.status == "pickup")
            {
                CalcDistanceAndGetZoneID(naviX, naviY, false);
            }
            else if (obj.status == "drop")
            {
                CalcDistanceAndGetZoneID(naviX, naviY, true);
            }



            /////////////////////////////
            // PICKUP 이벤트
            /////////////////////////////
            if (obj.status == "pickup")//지게차가 물건을 올렸을경우 선반 에서는 물건이 빠질경우
            {
                ActionInfoModel ActionObj = new ActionInfoModel();
                ActionObj.actionInfo.workLocationId = "WIS";
                ActionObj.actionInfo.vehicleId = m_vihicle;
                ActionObj.actionInfo.height = (m_Height_Distance_mm - 740).ToString();

                if (zoneId.Equals("") || zoneId == null)
                {
                    ActionObj.actionInfo.zoneId = "NA";
                }
                else
                {
                    ActionObj.actionInfo.zoneId = zoneId;
                }


                if (zoneName.Equals("") || zoneId == null)
                {
                    ActionObj.actionInfo.zoneName = "NA";
                }
                else
                {
                    ActionObj.actionInfo.zoneName = zoneName;
                }

                ActionObj.actionInfo.projectId = projectId;
                ActionObj.actionInfo.mappingId = mappingId;
                ActionObj.actionInfo.mapId = mapId;

                //Pattlite_Buzzer_LED(ePlayBuzzerLed.ACTION_START);
                m_qr = "";
                m_LoadMatrix = null;
                Tools.Log($"IN##########################pick up Action", Tools.ELogType.BackEndLog);
                Tools.Log($"IN##########################pick up Action", Tools.ELogType.WeightLog);
                Tools.Log($"Pickup Wait Delay {visionConfig.pickup_wait_delay} Second ", Tools.ELogType.BackEndLog);
                Thread.Sleep(visionConfig.pickup_wait_delay);
                Tools.Log($"Stop receive rack epc", Tools.ELogType.BackEndLog);
                m_stop_rack_epc = false;
                float rssi = (float)0.00;
                bool IsShelf = false;
                //string epc_data = GetMostRackEPC(ref IsShelf, rfidConfig.nRssi_pickup_timeout, rfidConfig.nRssi_pickup_threshold, ref rssi, m_Height_Distance_mm);
                //ActionObj.actionInfo.epc = epc_data;
                ActionObj.actionInfo.cepc = "NA";
                ActionObj.actionInfo.epc = "DP" + zoneName;

                //Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.action = "pickup";
                m_CalRate = CalcHeightLoadRate((int)obj.height);
                Tools.Log($"Rate : {obj.area}", Tools.ELogType.BackEndLog);
                Tools.Log($"Copy Before LoadRate  : {m_CalRate}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.loadRate = "0";
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";
                m_LoadMatrix = obj.matrix;

                if (obj.matrix != null)
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

                m_vision_width = ActionObj.actionInfo.visionWidth = obj.width;
                m_vision_height = ActionObj.actionInfo.visionHeight = obj.height;
                m_vision_depth = ActionObj.actionInfo.visionHeight = obj.depth;


                bool IsSendBackend = true;
                int nRet = WaitLoadSensor(_distance.pick_up_distance_threshold);
                Tools.Log($"loadweight_timeout {_weightConfig.loadweight_timeout} Second ", Tools.ELogType.BackEndLog);



                ActionObj.actionInfo.loadWeight = m_weight.GrossWeight;

                if (nRet == -1)
                {
                    Tools.Log($"Distance Fail", Tools.ELogType.BackEndLog);
                    IsSendBackend = false;
                }

                if (ActionObj.actionInfo.loadWeight <= 10)
                {
                    Tools.Log($"Weight Fail", Tools.ELogType.BackEndLog);
                    IsSendBackend = false;
                }

                if (obj.height <= 0)
                {
                    Tools.Log($"Hedight Fail", Tools.ELogType.BackEndLog);
                    IsSendBackend = false;

                }

                m_qr = obj.qr;
                m_qr = m_qr.Replace("{", "");
                m_qr = m_qr.Replace("}", "");
                m_qr = m_qr.Replace("wata", "");
                // m_container_qr = m_qr;
                bool IsQRCheckFail = false;

                m_container_qr = "NA";

                if (m_qr.Length == 32)
                {
                    m_container_qr = ActionObj.actionInfo.loadId = m_qr;
                    IsQRCheckFail = true;
                    Tools.Log($"QR Check OK", Tools.ELogType.BackEndLog);
                }
                else
                {
                    m_container_qr = m_qr = "NA";
                    IsQRCheckFail = false;
                    Tools.Log($"QR Check Failed.", Tools.ELogType.BackEndLog);
                }

                Tools.Log($"IS QR Check {IsQRCheckFail}.", Tools.ELogType.BackEndLog);

                if (m_Height_Distance_mm < 1500)
                {
                    Tools.Log($"@@shelf false", Tools.ELogType.BackEndLog);
                    if (IsShelf == true)
                    {
                        Tools.Log($"##roof is visible", Tools.ELogType.BackEndLog);
                        ActionObj.actionInfo.shelf = true;
                    }
                    else
                    {
                        Tools.Log($"##roof is not visible", Tools.ELogType.BackEndLog);
                        ActionObj.actionInfo.shelf = false;
                    }
                }
                else
                {

                    Tools.Log($"@@shelf true", Tools.ELogType.BackEndLog);
                    ActionObj.actionInfo.shelf = true;
                }

                if (IsSendBackend)
                {
                    Tools.Log($"Pickup ##QR : {m_qr}", Tools.ELogType.BackEndLog);

                    string json_body = Util.ObjectToJson(ActionObj);
                    RestClientPostModel post_obj = new RestClientPostModel();
                    post_obj.body = json_body;
                    post_obj.type = eMessageType.BackEndAction;
                    post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";

                    Tools.Log($"[PICK-UP] {json_body}", Tools.ELogType.BackEndLog);
                    _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

                }
                else
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.ACTION_FAIL);

                }
                ClearEpc();
                m_stop_rack_epc = true;

                SendToIndicator(m_weight.GrossWeight, m_weight.RightWeight, m_weight.RightWeight, m_qr, m_vision_width, m_vision_height, m_vision_depth);

                Tools.Log("start receive rack epc", Tools.ELogType.BackEndLog);
                //Tools.Log($"Action : [pickup] EPC  [{epc_data}] Rssi : [{rssi}] QR {m_qr} ", Tools.ELogType.ActionLog);

            }
            else if (obj.status == "drop")//지게차가 물건을 놨을경우  선반 에서는 물건이 추가될 경우
            {
                ActionInfoModel ActionObj = new ActionInfoModel();
                ActionObj.actionInfo.workLocationId = m_location;
                ActionObj.actionInfo.vehicleId = m_vihicle;
                ActionObj.actionInfo.height = m_Height_Distance_mm.ToString();

                //float rssi = (float)0.00;

                //zoneId = "45c3293c429fe32a926176eff563d0ad";
                //zoneName = "PLL-D-1";


                Pattlite_Buzzer_LED(ePlayBuzzerLed.DROP);

                Tools.Log($"OUT##########################Drop Action", Tools.ELogType.WeightLog);
                Tools.Log($"Stop receive rack epc", Tools.ELogType.BackEndLog);
                m_stop_rack_epc = false;
                bool IsShelf = false;
                //string epc_data = GetMostRackEPC(ref IsShelf, rfidConfig.nRssi_drop_timeout, rfidConfig.nRssi_drop_threshold, ref rssi, m_Height_Distance_mm);
                //ActionObj.actionInfo.epc = epc_data;
                ActionObj.actionInfo.cepc = "NA";
                ActionObj.actionInfo.epc = "DP" + zoneName;

                //Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);

                ActionObj.actionInfo.action = "drop";
                ActionObj.actionInfo.loadRate = m_CalRate.ToString();
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";

                ActionObj.actionInfo.visionWidth = m_vision_width;
                ActionObj.actionInfo.visionHeight = m_vision_height;
                ActionObj.actionInfo.visionDepth = m_vision_depth;


                if (zoneId.Equals("") || zoneId == null)
                {
                    ActionObj.actionInfo.zoneId = "NA";
                }
                else
                {
                    ActionObj.actionInfo.zoneId = zoneId;
                }


                if (zoneName.Equals("") || zoneId == null)
                {
                    ActionObj.actionInfo.zoneName = "NA";
                }
                else
                {
                    ActionObj.actionInfo.zoneName = zoneName;
                }


                ActionObj.actionInfo.projectId = projectId;
                ActionObj.actionInfo.mappingId = mappingId;
                ActionObj.actionInfo.mapId = mapId;

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

                if (m_Height_Distance_mm < 1500)
                {

                    if (IsShelf == true)
                    {
                        Tools.Log($"##roof is visible", Tools.ELogType.BackEndLog);
                        ActionObj.actionInfo.shelf = true;
                    }
                    else
                    {
                        Tools.Log($"##roof is not visible", Tools.ELogType.BackEndLog);
                        ActionObj.actionInfo.shelf = false;
                    }

                }
                else
                {
                    Tools.Log($"@@shelf true", Tools.ELogType.BackEndLog);
                    ActionObj.actionInfo.shelf = true;
                }

                //Tools.Log($"!####[drop Rack Event] {epc_data}", Tools.ELogType.BackEndLog);
                Tools.Log($"!#### LoadRate  : {ActionObj.actionInfo.loadRate}", Tools.ELogType.BackEndLog);
                Tools.Log($"!#### QR  : {m_qr}", Tools.ELogType.BackEndLog);


                Tools.Log($"##QR : {m_qr}", Tools.ELogType.BackEndLog);

                ActionObj.actionInfo.loadId = m_qr;

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
                Tools.Log($"[PICK-DROP] {json_body}", Tools.ELogType.BackEndLog);
                _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);


                ClearEpc();
                m_CalRate = 0;
                m_vision_width = 0;
                m_vision_height = 0;
                m_vision_depth = 0;
                m_qr = "";
                Tools.Log("Clear LoadRate", Tools.ELogType.BackEndLog);
                m_stop_rack_epc = true;
                Tools.Log("start receive rack epc", Tools.ELogType.BackEndLog);
                //Tools.Log($"Action : [drop] EPC  [{epc_data}] Rssi : [{rssi}] QR {m_qr} ", Tools.ELogType.ActionLog);


                SendToIndicator(0, 0, 0, "", 0, 0, 0);
            }
            else
            {
                Tools.Log("Action Idle", Tools.ELogType.BackEndLog);
            }
        }
    }
}
