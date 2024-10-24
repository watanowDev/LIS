using ControlzEx.Standard;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Layout;
using Newtonsoft.Json.Linq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.DPS;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.StatusLED;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.DPS;
using WATA.LIS.Core.Model.ErrorCheck;
using WATA.LIS.Core.Model.Indicator;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.VISION;
using Windows.Security.Cryptography.Core;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.Core.Services
{
    /*
     * StatusService_Pantos 구성요소
     * 거리센서 : TeraBee EVO-15 (높이 측정)
     * RFID : A pluse RFID 수신기 
     * VISION : Astra-FemtoW
     */

    public class StatusService_WIS_KINTEX : IStatusService
    {
        IEventAggregator _eventAggregator;

        public int      m_Height_Distance_mm { get; set; }
 
        private int rifid_status_check_count = 0;
        private int distance_status_check_count = 35;
        private int status_limit_count = 10;

        private string m_location = "WIS";
        private string m_vihicle = "fork_lift001";
        private string m_errorcode = "0000";

        private bool m_stop_rack_epc = true;
        private RFIDConfigModel rfidConfig;

        private VisionConfigModel visionConfig;

        private WeightSensorModel m_weight;
        WeightConfigModel _weightConfig;
        DistanceConfigModel  _distance;
 


        private readonly IWeightModel _weightmodel;
        DispatcherTimer BuzzerTimer;


        private string _EPC_DATA = "DA00025C00020000000200ED";
        private string _QR = "watad7d7a690ecbb4b3090102f88605f9b5e";
        public StatusService_WIS_KINTEX(IEventAggregator eventAggregator , IMainModel main, IRFIDModel rfidmodel, IVisionModel visionModel, IWeightModel weightmodel, IDistanceModel distanceModel)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RackProcess_Event>().Subscribe(OnRFIDLackData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<LocationProcess_Event>().Subscribe(OnLocationData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<VISION_Event>().Subscribe(OnVISIONEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<WeightSensorEvent>().Subscribe(OnWeightSensor, ThreadOption.BackgroundThread, true);
            //_eventAggregator.GetEvent<WeightSensorTrigger>().Subscribe(OnWeightTrigger, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<BackEndReturnCodeEvent>().Subscribe(OnContainerReturn, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<IndicatorRecvEvent>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<SimulModeEvent>().Subscribe(OnSimulEPC, ThreadOption.BackgroundThread, true);



            _weightmodel = weightmodel;
            _distance = (DistanceConfigModel) distanceModel;

            _weightConfig = (WeightConfigModel)_weightmodel;


           // DispatcherTimer StatusClearTimer = new DispatcherTimer();
           // StatusClearTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            //StatusClearTimer.Tick += new EventHandler(StatusClearEvent);
           // StatusClearTimer.Start();

            DispatcherTimer CurrentTimer = new DispatcherTimer();
            CurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            CurrentTimer.Tick += new EventHandler(CurrentLocationTimerEvent);
            CurrentTimer.Start();
            MainConfigModel mainobj = (MainConfigModel)main;
            m_vihicle = mainobj.vehicleId;
            visionConfig =(VisionConfigModel) visionModel;
            rfidConfig = (RFIDConfigModel)rfidmodel;


            DispatcherTimer ErrorCheckTimer = new DispatcherTimer();
            ErrorCheckTimer.Interval = new TimeSpan(0, 0, 0, 0, 2000);
            ErrorCheckTimer.Tick += new EventHandler(StatusErrorCheckEvent);
            ErrorCheckTimer.Start();



            //DispatcherTimer AliveTimer = new DispatcherTimer();
            //AliveTimer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            //AliveTimer.Tick += new EventHandler(AliveTimerEvent);
           // AliveTimer.Start();



            BuzzerTimer = new DispatcherTimer();
            BuzzerTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            BuzzerTimer.Tick += new EventHandler(BuzzerTimerEvent);


            DispatcherTimer IndicatorTimer = new DispatcherTimer();
            IndicatorTimer.Interval = new TimeSpan(0, 0, 0, 0, 300);
            IndicatorTimer.Tick += new EventHandler(IndicatorSendTimerEvent);
            IndicatorTimer.Start();


            Tools.Log($"Start Status Service", Tools.ELogType.SystemLog);


             m_weight = new WeightSensorModel();

        }


        private void DPS_CLEAR()
        {
            DPSAllClearModel model_obj = new DPSAllClearModel();
            model_obj.payload.AckType = 0; //REQUEST
            model_obj.payload.ControllerID = 30;
            model_obj.payload.LocationID = 233;
            byte[] target = Util.ObjectToByte(model_obj);
            _eventAggregator.GetEvent<DPSSendEvent>().Publish(target);
        }


        private void DPS_CLEAR2()
        {
            SetDisplayModel SetDisplay_obj = new SetDisplayModel();


            SetDisplay_obj.payload.AckType = 0;
            SetDisplay_obj.payload.LocationID = 0x30;
            SetDisplay_obj.payload.ControllerID = 0x33;
            SetDisplay_obj.payload.ADDR1 = 1;
            SetDisplay_obj.payload.ADDR2 = 0;
            SetDisplay_obj.payload.SEQ = 0;
            SetDisplay_obj.payload.COLORSET1 = 0;
            SetDisplay_obj.payload.COLORSET2 = 0;
            SetDisplay_obj.payload.COLORSET3 = 0;
            SetDisplay_obj.payload.COLORSET4 = 0;
            SetDisplay_obj.payload.COLORSET5 = 0;
            SetDisplay_obj.payload.COLORSET6 = 0;
            SetDisplay_obj.payload.COLORSET7 = 0;
            SetDisplay_obj.payload.COLORSET8 = 0;
            SetDisplay_obj.payload.COLORSET9 = 0;
            SetDisplay_obj.payload.COLORSET10 = 0;
            SetDisplay_obj.payload.COLORSET11 = 0;
            SetDisplay_obj.payload.COLORSET12 = 0;
            SetDisplay_obj.payload.COLORSET13 = 0;
            SetDisplay_obj.payload.COLORSET14 = 0;
            SetDisplay_obj.payload.COLORSET15 = 0;
            SetDisplay_obj.payload.COLORSET16 = 0;

            SetDisplay_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            SetDisplay_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
            SetDisplay_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
            SetDisplay_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            SetDisplay_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
            SetDisplay_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
            SetDisplay_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            SetDisplay_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
            SetDisplay_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
            SetDisplay_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            SetDisplay_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
            SetDisplay_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
            SetDisplay_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            SetDisplay_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
            SetDisplay_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
            SetDisplay_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes("대"));

            byte[] SetDisplay_buffer = Util.ObjectToByte(SetDisplay_obj);
            _eventAggregator.GetEvent<DPSSendEvent>().Publish(SetDisplay_buffer);

        }


        private int _DPS_INDEX = 2;

        private void DPS_EVENT()
        {
            Tools.Log($"DPS_EVENT", Tools.ELogType.DPSLog);
            Thread.Sleep(1000);
            DPS_CLEAR2();
            Thread.Sleep(2000);
            SetDisplay("완","료", _DPS_INDEX);
            Thread.Sleep(10000);
            //DPS_CLEAR2();
            Thread.Sleep(3000);
            SetDisplay("대","기", _DPS_INDEX);
            Thread.Sleep(1000);
            //DPS_CLEAR2();
            SetDisplay("입","고" ,"2", _DPS_INDEX);
        }

        private void DPS_EVENT2(string a1)
        {
            Tools.Log($"DPS_EVENT2", Tools.ELogType.DPSLog);
            Thread.Sleep(1000);
            DPS_CLEAR2();
            Thread.Sleep(2000);
            SetDisplay("완", "료", _DPS_INDEX);

            
            //Thread.Sleep(1000);
            //DPS_CLEAR2();
            Thread.Sleep(1000);
            SetDisplay("대", "기", _DPS_INDEX);
            Thread.Sleep(1000);
            //DPS_CLEAR2();
            //Thread.Sleep(1000);
            SetDisplay("출", "고" ,"2", _DPS_INDEX);
        }



        private void SetDisplay(string a1, string a2 ,int index)
        {
            SetDisplayModel SetDisplay_obj = new SetDisplayModel();

            if(index == 1)
            {
                SetDisplay_obj.payload.AckType = 0;
                SetDisplay_obj.payload.LocationID = 0x30;
                SetDisplay_obj.payload.ControllerID = 0x33;
                SetDisplay_obj.payload.ADDR1 = 1;
                SetDisplay_obj.payload.ADDR2 = 0;
                SetDisplay_obj.payload.SEQ = 0;
                SetDisplay_obj.payload.COLORSET1 = 2;
                SetDisplay_obj.payload.COLORSET2 = 2;
                SetDisplay_obj.payload.COLORSET3 = 2;
                SetDisplay_obj.payload.COLORSET4 = 3;
                SetDisplay_obj.payload.COLORSET5 = 3;
                SetDisplay_obj.payload.COLORSET6 = 3;
                SetDisplay_obj.payload.COLORSET7 = 3;
                SetDisplay_obj.payload.COLORSET8 = 3;
                SetDisplay_obj.payload.COLORSET9 = 3;
                SetDisplay_obj.payload.COLORSET10 = 0;
                SetDisplay_obj.payload.COLORSET11 = 0;
                SetDisplay_obj.payload.COLORSET12 = 0;
                SetDisplay_obj.payload.COLORSET13 = 0;
                SetDisplay_obj.payload.COLORSET14 = 0;
                SetDisplay_obj.payload.COLORSET15 = 0;
                SetDisplay_obj.payload.COLORSET16 = 0;

                SetDisplay_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes(a1));
                SetDisplay_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes(a2));
                SetDisplay_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes("1"));
                SetDisplay_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes("1"));
                SetDisplay_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            }

            else if (index == 2)
            {

                SetDisplay_obj.payload.AckType = 0;
                SetDisplay_obj.payload.LocationID = 0x30;
                SetDisplay_obj.payload.ControllerID = 0x33;
                SetDisplay_obj.payload.ADDR1 = 1;
                SetDisplay_obj.payload.ADDR2 = 0;
                SetDisplay_obj.payload.SEQ = 0;
                SetDisplay_obj.payload.COLORSET1 = 3;
                SetDisplay_obj.payload.COLORSET2 = 3;
                SetDisplay_obj.payload.COLORSET3 = 3;
                SetDisplay_obj.payload.COLORSET4 = 2;
                SetDisplay_obj.payload.COLORSET5 = 2;
                SetDisplay_obj.payload.COLORSET6 = 2;
                SetDisplay_obj.payload.COLORSET7 = 3;
                SetDisplay_obj.payload.COLORSET8 = 3;
                SetDisplay_obj.payload.COLORSET9 = 3;
                SetDisplay_obj.payload.COLORSET10 = 0;
                SetDisplay_obj.payload.COLORSET11 = 0;
                SetDisplay_obj.payload.COLORSET12 = 0;
                SetDisplay_obj.payload.COLORSET13 = 0;
                SetDisplay_obj.payload.COLORSET14 = 0;
                SetDisplay_obj.payload.COLORSET15 = 0;
                SetDisplay_obj.payload.COLORSET16 = 0;

                SetDisplay_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes(a1));
                SetDisplay_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes(a2));
                SetDisplay_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes("1"));
                SetDisplay_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes("1"));
                SetDisplay_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            }
            else if(index == 3)
            {
                    SetDisplay_obj.payload.AckType = 0;
                    SetDisplay_obj.payload.LocationID = 0x30;
                    SetDisplay_obj.payload.ControllerID = 0x33;
                    SetDisplay_obj.payload.ADDR1 = 1;
                    SetDisplay_obj.payload.ADDR2 = 0;
                    SetDisplay_obj.payload.SEQ = 0;
                    SetDisplay_obj.payload.COLORSET1 = 3;
                    SetDisplay_obj.payload.COLORSET2 = 3;
                    SetDisplay_obj.payload.COLORSET3 = 3;
                    SetDisplay_obj.payload.COLORSET4 = 3;
                    SetDisplay_obj.payload.COLORSET5 = 3;
                    SetDisplay_obj.payload.COLORSET6 = 3;
                    SetDisplay_obj.payload.COLORSET7 = 2;
                    SetDisplay_obj.payload.COLORSET8 = 2;
                    SetDisplay_obj.payload.COLORSET9 = 2;
                    SetDisplay_obj.payload.COLORSET10 = 0;
                    SetDisplay_obj.payload.COLORSET11 = 0;
                    SetDisplay_obj.payload.COLORSET12 = 0;
                    SetDisplay_obj.payload.COLORSET13 = 0;
                    SetDisplay_obj.payload.COLORSET14 = 0;
                    SetDisplay_obj.payload.COLORSET15 = 0;
                    SetDisplay_obj.payload.COLORSET16 = 0;

                    SetDisplay_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                    SetDisplay_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                    SetDisplay_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                    SetDisplay_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                    SetDisplay_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                    SetDisplay_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                    SetDisplay_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes(a1));
                    SetDisplay_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes(a2));
                    SetDisplay_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                    SetDisplay_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                    SetDisplay_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                    SetDisplay_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes("1"));
                    SetDisplay_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                    SetDisplay_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                    SetDisplay_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes("1"));
                    SetDisplay_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            }

            byte[] SetDisplay_buffer = Util.ObjectToByte(SetDisplay_obj);
            _eventAggregator.GetEvent<DPSSendEvent>().Publish(SetDisplay_buffer);

        }

        private void SetDisplay(string a1, string a2, string a3 , int index)
        {
            SetDisplayModel SetDisplay_obj = new SetDisplayModel();

            if (index == 1)
            {
                SetDisplay_obj.payload.AckType = 0;
                SetDisplay_obj.payload.LocationID = 0x30;
                SetDisplay_obj.payload.ControllerID = 0x33;
                SetDisplay_obj.payload.ADDR1 = 1;
                SetDisplay_obj.payload.ADDR2 = 0;
                SetDisplay_obj.payload.SEQ = 0;
                SetDisplay_obj.payload.COLORSET1 = 3;
                SetDisplay_obj.payload.COLORSET2 = 3;
                SetDisplay_obj.payload.COLORSET3 = 3;
                SetDisplay_obj.payload.COLORSET4 = 3;
                SetDisplay_obj.payload.COLORSET5 = 3;
                SetDisplay_obj.payload.COLORSET6 = 3;
                SetDisplay_obj.payload.COLORSET7 = 2;
                SetDisplay_obj.payload.COLORSET8 = 2;
                SetDisplay_obj.payload.COLORSET9 = 2;
                SetDisplay_obj.payload.COLORSET10 = 0;
                SetDisplay_obj.payload.COLORSET11 = 0;
                SetDisplay_obj.payload.COLORSET12 = 0;
                SetDisplay_obj.payload.COLORSET13 = 0;
                SetDisplay_obj.payload.COLORSET14 = 0;
                SetDisplay_obj.payload.COLORSET15 = 0;
                SetDisplay_obj.payload.COLORSET16 = 0;

                SetDisplay_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes(a1));
                SetDisplay_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes(a2));
                SetDisplay_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(a3));
                SetDisplay_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            }
            else if (index == 2)
            {
                SetDisplay_obj.payload.AckType = 0;
                SetDisplay_obj.payload.LocationID = 0x30;
                SetDisplay_obj.payload.ControllerID = 0x33;
                SetDisplay_obj.payload.ADDR1 = 1;
                SetDisplay_obj.payload.ADDR2 = 0;
                SetDisplay_obj.payload.SEQ = 0;
                SetDisplay_obj.payload.COLORSET1 = 3;
                SetDisplay_obj.payload.COLORSET2 = 3;
                SetDisplay_obj.payload.COLORSET3 = 3;
                SetDisplay_obj.payload.COLORSET4 = 2;
                SetDisplay_obj.payload.COLORSET5 = 2;
                SetDisplay_obj.payload.COLORSET6 = 2;
                SetDisplay_obj.payload.COLORSET7 = 3;
                SetDisplay_obj.payload.COLORSET8 = 3;
                SetDisplay_obj.payload.COLORSET9 = 3;
                SetDisplay_obj.payload.COLORSET10 = 0;
                SetDisplay_obj.payload.COLORSET11 = 0;
                SetDisplay_obj.payload.COLORSET12 = 0;
                SetDisplay_obj.payload.COLORSET13 = 0;
                SetDisplay_obj.payload.COLORSET14 = 0;
                SetDisplay_obj.payload.COLORSET15 = 0;
                SetDisplay_obj.payload.COLORSET16 = 0;

                SetDisplay_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes(a1));
                SetDisplay_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes(a2));
                SetDisplay_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(a3));
                SetDisplay_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            }
            else if (index == 3)
            {
                SetDisplay_obj.payload.AckType = 0;
                SetDisplay_obj.payload.LocationID = 0x30;
                SetDisplay_obj.payload.ControllerID = 0x33;
                SetDisplay_obj.payload.ADDR1 = 1;
                SetDisplay_obj.payload.ADDR2 = 0;
                SetDisplay_obj.payload.SEQ = 0;
                SetDisplay_obj.payload.COLORSET1 = 3;
                SetDisplay_obj.payload.COLORSET2 = 3;
                SetDisplay_obj.payload.COLORSET3 = 3;
                SetDisplay_obj.payload.COLORSET4 = 2;
                SetDisplay_obj.payload.COLORSET5 = 2;
                SetDisplay_obj.payload.COLORSET6 = 2;
                SetDisplay_obj.payload.COLORSET7 = 3;
                SetDisplay_obj.payload.COLORSET8 = 3;
                SetDisplay_obj.payload.COLORSET9 = 3;
                SetDisplay_obj.payload.COLORSET10 = 0;
                SetDisplay_obj.payload.COLORSET11 = 0;
                SetDisplay_obj.payload.COLORSET12 = 0;
                SetDisplay_obj.payload.COLORSET13 = 0;
                SetDisplay_obj.payload.COLORSET14 = 0;
                SetDisplay_obj.payload.COLORSET15 = 0;
                SetDisplay_obj.payload.COLORSET16 = 0;

                SetDisplay_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes(a1));
                SetDisplay_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes(a2));
                SetDisplay_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(a3));
                SetDisplay_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
                SetDisplay_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes("기"));
                SetDisplay_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes(" "));
                SetDisplay_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes("대"));
            }
            byte[] SetDisplay_buffer = Util.ObjectToByte(SetDisplay_obj);
            _eventAggregator.GetEvent<DPSSendEvent>().Publish(SetDisplay_buffer);






        }



        private byte[] ConvertASCII(byte[] temp)
        {

            byte[] ret = new byte[3];



            if (temp.Length == 3)
            {
                ret[0] = temp[0];
                ret[1] = temp[1];
                ret[2] = temp[2];
            }
            else if (temp.Length == 2)
            {
                ret[0] = 0x00;
                ret[1] = temp[0];
                ret[2] = temp[1];
            }
            else if (temp.Length == 1)
            {
                ret[0] = 0x00;
                ret[1] = 0x00;
                ret[2] = temp[0];
            }

            return ret;
        }

        private string m_Location_epc = "";

        public void OnWeightSensor(WeightSensorModel obj)
        {

            m_weight = obj;
            Tools.Log($"Weight {m_weight}", Tools.ELogType.SystemLog);
        }


        public void OnContainerReturn(int status)
        {
            if(status != 200)
            {
                Pattlite_Buzzer_LED(ePlayBuzzerLed.EMERGENCY2);
                Tools.Log($"IN########################## EMERGENCY2", Tools.ELogType.BackEndLog);
            }
        }


        public void OnSimulEPC(SimulationModel obj)
        {
            _EPC_DATA = obj.EPC;
            
            if(obj.EPC == "DA00025C00020000000100ED")
            {
                _DPS_INDEX = 3;
            }
            else if (obj.EPC == "DA00025C00020000000200ED")
            {
                _DPS_INDEX = 2;
            }
            else if (obj.EPC == "DA00025C00020000000300ED")
            {
                _DPS_INDEX = 1;
            }

            Tools.Log($"######Set Simulation EPC !!!!! {_EPC_DATA} index {_DPS_INDEX }", Tools.ELogType.BackEndLog);


        }



        public void OnIndicatorEvent(string status)
        {
            Tools.Log($"OnIndicatorEvent1111 {status}", Tools.ELogType.DisplayLog);

            if (status == "pick_up")
            {
                Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_MEASRUE_OK);
                
                if (m_pickup_obj != null)
                {

                 //   PickUpEvent(m_pickup_obj);
                }
            }
        }


        public void OnWeightTrigger(string str)
        {
         

        }

       
        private void CurrentLocationTimerEvent(object sender, EventArgs e)
        {
            string epc = _EPC_DATA;//GetMostlocationEPC(1, 0);


            if (epc.Contains("DA"))
            {

                LocationInfoModel location_obj = new LocationInfoModel();

                location_obj.locationInfo.vehicleId = m_vihicle;
                location_obj.locationInfo.workLocationId = m_location;

                m_Location_epc = location_obj.locationInfo.epc = epc;


                string json_body = Util.ObjectToJson(location_obj);
                RestClientPostModel post_obj = new RestClientPostModel();
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndCurrent;
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/location";

                _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

            }
            else if (epc.Contains("DC"))
            {
                ContainerGateEventModel GateEventModelobj = new ContainerGateEventModel();
                GateEventModelobj.containerInfo.vehicleId = m_vihicle;
                GateEventModelobj.containerInfo.cepc = epc;


                if (m_container_qr != "NA")
                {

                    Tools.Log($"Send Container {m_container_qr} ", Tools.ELogType.ActionLog);
                    Tools.Log($"Send Container {m_container_qr} ", Tools.ELogType.BackEndLog);
                    GateEventModelobj.containerInfo.loadId = m_container_qr;

              
                    string json_body = Util.ObjectToJson(GateEventModelobj);
                    RestClientPostModel post_obj = new RestClientPostModel();
                  
                    post_obj.body = json_body;
                    post_obj.type = eMessageType.BackEndContainer;
                    post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/container-gate-event";

                    Tools.Log($"TRUE URL : {post_obj.url} ", Tools.ELogType.ActionLog);
                    Tools.Log($"TRUE URL : {post_obj.url} ", Tools.ELogType.BackEndLog);


                    Tools.Log($"TRUE Body : {json_body} ", Tools.ELogType.ActionLog);
                    Tools.Log($"TRUE Body : {json_body} ", Tools.ELogType.BackEndLog);
                    _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);

                }
                else
                {
                    Tools.Log($"NA Send Container {m_container_qr} ", Tools.ELogType.ActionLog);
                    Tools.Log($"NA Send Container {m_container_qr} ", Tools.ELogType.BackEndLog);
                    GateEventModelobj.containerInfo.loadId = m_container_qr;


                    string json_body = Util.ObjectToJson(GateEventModelobj);
                    RestClientPostModel post_obj = new RestClientPostModel();
                    post_obj.body = json_body;
                    post_obj.type = eMessageType.BackEndContainer;
                    post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/container-gate-event";

                    Tools.Log($"NA URL : {post_obj.url} ", Tools.ELogType.ActionLog);
                    Tools.Log($"NA URL : {post_obj.url} ", Tools.ELogType.BackEndLog);


                    Tools.Log($"NA Body : {json_body} ", Tools.ELogType.ActionLog);
                    Tools.Log($"NA Body : {json_body} ", Tools.ELogType.BackEndLog);
                    _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);
    
                }
            }
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
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
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



                if(GlobalValue.IS_ERROR.distance == false || 
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

            



            if(rifid_status_check_count >  status_limit_count)
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

                //DistanceSensorModel DisTanceObject = new DistanceSensorModel();
                //m_Height_Distance_mm = -100;
                //DisTanceObject.Distance_mm = m_Height_Distance_mm;
                //_eventAggregator.GetEvent<DistanceSensorEvent>().Publish(DisTanceObject);
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

        private string GetMostRackEPC(ref bool shelf, int TimeSec,float Threshold ,ref float rssi, int H_distance)
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

                shelf = true;


                if (retRFIDInfoList.Count > 0)
                {
                    retKeys = retRFIDInfoList.Aggregate((x, y) => x.Value.RSSI > y.Value.RSSI ? x : y).Key;

                    if (retRFIDInfoList.TryGetValue(retKeys, out EPC_Value_Model Temp))
                    {
                        Tools.Log($"EPCKey Count {Temp.EPC_Check_Count}", Tools.ELogType.BackEndLog);
                        Tools.Log($"EPCKey RSSI {Temp.RSSI}", Tools.ELogType.BackEndLog);

                        if (rssi < Threshold )
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

            while(true)
            {
                if(count > 100)
                {
                    nRet = -1;
                    break;
                }

                if(m_Height_Distance_mm > distanceThreshold)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.ACTION_START);

                    Thread.Sleep(1000);


                    Thread.Sleep(_weightConfig.loadweight_timeout);

                    nRet = 0;
                    break;
                }


                Thread.Sleep(100);
                count++;

            }
            return nRet;
        }

        private int WaitLoadSensor_weight()
        {
            int count = 0;
            int nRet = -1;

            while (true)
            {
                if (count > 90)
                {
                    Tools.Log($"weight check Time out", Tools.ELogType.SystemLog);


                    Pattlite_Buzzer_LED(ePlayBuzzerLed.QR_MEASRUE_OK);

                    Thread.Sleep(500);



                    _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.weight_check_complete);

                    nRet = -1;
                    break;
                }

                if (m_weight.GrossWeight > 10)
                {
                    Pattlite_Buzzer_LED(ePlayBuzzerLed.ACTION_START);

                    Thread.Sleep(1000);

                    _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.weight_check_complete);

                    nRet = 0;
                    break;
                }


                Thread.Sleep(100);
                count++;

            }
            return nRet;
        }




        public void OnLocationData(LocationRFIDEventModel obj)
        {
            if(Is_front_ant_disable)
            {
                QueryRFIDModel epcModel = new QueryRFIDModel();
                // epcModel.EPC = obj.EPC;
                epcModel.EPC = _EPC_DATA;
                epcModel.Time = DateTime.Now;
                epcModel.RSSI = -65; ;
                m_location_epclist.Add(epcModel);

                Tools.Log($"Location EPC Receive {obj.EPC}", Tools.ELogType.SystemLog);
            }
        }

        public void OnRFIDLackData(RackRFIDEventModel obj)
        {
            rifid_status_check_count = 0;//erase status clear

            QueryRFIDModel epcModel = new QueryRFIDModel();
            epcModel.EPC = _EPC_DATA;
            
            epcModel.Time = DateTime.Now;
            epcModel.RSSI = -70;

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

        int m_drop_weight = 0;
        bool _shelf = true;


        public void PickUpEvent(VISON_Model obj)
        {
            m_drop_weight = 0;

            ActionInfoModel ActionObj = new ActionInfoModel();
            ActionObj.actionInfo.workLocationId = m_location;
            ActionObj.actionInfo.vehicleId = m_vihicle;

            _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.dummy);
            Thread.Sleep(500);

            if (obj.simulation_status == "F_OUT")
            {

                m_Height_Distance_mm = 500;
                m_weight.RightWeight = 50;
                m_weight.LeftWeight = 50;
                m_weight.GrossWeight = 100;

            }
            else if (obj.simulation_status == "F_IN")
            {
                m_weight.RightWeight = 50;
                m_weight.LeftWeight = 50;
                m_weight.GrossWeight = 100;
                m_Height_Distance_mm = 500;
            }
            else if (obj.simulation_status == "OUT")
            {
                m_weight.RightWeight = 50;
                m_weight.LeftWeight = 50;
                m_weight.GrossWeight = 100;
                m_Height_Distance_mm = 1700;
            }
            else if (obj.simulation_status == "IN")
            {
                m_weight.RightWeight = 50;
                m_weight.LeftWeight = 50;
                m_weight.GrossWeight = 100;
                m_Height_Distance_mm = 1700;
            }

            ActionObj.actionInfo.height = m_Height_Distance_mm.ToString();

            if (m_Height_Distance_mm < 1300)
            {


                _shelf = ActionObj.actionInfo.shelf = false;

            }
            else
            {

                Tools.Log($"@@shelf true", Tools.ELogType.BackEndLog);
                _shelf = ActionObj.actionInfo.shelf = true;
            }


            _eventAggregator.GetEvent<SpeakerInfoEvent>().Publish(ePlayInfoSpeaker.size_check_complete);



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
            // string epc_data = GetMostRackEPC(ref IsShelf, rfidConfig.nRssi_pickup_timeout, rfidConfig.nRssi_pickup_threshold, ref rssi, m_Height_Distance_mm);
            ActionObj.actionInfo.epc = _EPC_DATA;
            Tools.Log($"##rftag epc  : {_EPC_DATA}", Tools.ELogType.BackEndLog);
            ActionObj.actionInfo.action = "IN";
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
            m_vision_depth = ActionObj.actionInfo.visionDepth = obj.depth;




            bool IsSendBackend = true;


            WaitLoadSensor_weight();

            Tools.Log($"loadweight_timeout {_weightConfig.loadweight_timeout} Second ", Tools.ELogType.BackEndLog);



            Tools.Log($"loadweight_timeout {m_weight.GrossWeight}", Tools.ELogType.BackEndLog);


            ActionObj.actionInfo.loadWeight = m_weight.GrossWeight;
            ActionObj.actionInfo.loadWeight = m_weight.GrossWeight;
            m_drop_weight = m_weight.GrossWeight;
            m_qr = obj.qr = _QR;
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


            Thread.Sleep(500);

            
            
            


            Tools.Log($"Pickup ##QR : {m_qr}", Tools.ELogType.BackEndLog);

            string json_body = Util.ObjectToJson(ActionObj);
            RestClientPostModel post_obj = new RestClientPostModel();

            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Publish(post_obj);


            
            ClearEpc();
            m_stop_rack_epc = true;
            SendToIndicator(m_weight.GrossWeight, m_weight.RightWeight, m_weight.RightWeight, m_qr, m_vision_width, m_vision_height, m_vision_depth);
            Tools.Log("start receive rack epc", Tools.ELogType.BackEndLog);
            Tools.Log($"Action : [pickup] EPC  [{_EPC_DATA}] Rssi : [{rssi}] QR {m_qr} ", Tools.ELogType.ActionLog);

            m_pickup_obj = null;
        }


        VISON_Model m_pickup_obj;

        private int m_CalRate = 0;

        private byte[] m_LoadMatrix = new byte[10];
        private float m_vision_width = 0;
        private float m_vision_height = 0;
        private float m_vision_depth = 0;

        private string m_qr = "";
        private string m_container_qr = "";
        public void OnVISIONEvent(VISON_Model obj)
        {
            

         

            if (obj.status == "pickup")//지게차가 물건을 올렸을경우 선반 에서는 물건이 빠질경우
            {
                m_pickup_obj = obj;

                PickUpEvent(m_pickup_obj);

                
                bool IsQRCheckFail = false;
                m_vision_width =  obj.width;
                m_vision_height = obj.height;
                m_vision_depth = obj.depth;


                m_qr = obj.qr.Replace("{", "");
                m_qr = obj.qr.Replace("}", "");
                m_qr = obj.qr.Replace("wata", "");


                
                Tools.Log($"QR {m_qr}", Tools.ELogType.BackEndLog);
                /*

                if (m_qr.Length == 32)
                {
                    IsQRCheckFail = true;
                    Tools.Log($"QR Check OK", Tools.ELogType.BackEndLog);
                }
                else
                {
                    IsQRCheckFail = false;
                    Tools.Log($"QR Check Failed.", Tools.ELogType.BackEndLog);
                }
                */

                Pattlite_Buzzer_LED(ePlayBuzzerLed.ACTION_FINISH);

                if(_shelf == true)
                {
                    DPS_EVENT();
                }
            }
            else if(obj.status == "drop")//지게차가 물건을 놨을경우  선반 에서는 물건이 추가될 경우
            {
                ActionInfoModel ActionObj = new ActionInfoModel();
                ActionObj.actionInfo.workLocationId = m_location;
                ActionObj.actionInfo.vehicleId = m_vihicle;
                ActionObj.actionInfo.height = m_Height_Distance_mm.ToString();



                float rssi = (float)0.00;
                Pattlite_Buzzer_LED(ePlayBuzzerLed.DROP);
                Tools.Log($"OUT##########################Drop Action", Tools.ELogType.WeightLog);
                Tools.Log($"Stop receive rack epc", Tools.ELogType.BackEndLog);
                m_stop_rack_epc = false;
                //bool IsShelf = false;
                string epc_data = _EPC_DATA; //= GetMostRackEPC(ref IsShelf, rfidConfig.nRssi_drop_timeout, rfidConfig.nRssi_drop_threshold, ref rssi, m_Height_Distance_mm);
                ActionObj.actionInfo.epc = epc_data;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.action = "OUT";
                ActionObj.actionInfo.loadRate = m_CalRate.ToString();
                ActionObj.actionInfo.loadMatrixRaw = "10";
                ActionObj.actionInfo.loadMatrixColumn = "10";

                ActionObj.actionInfo.visionWidth = m_vision_width;
                ActionObj.actionInfo.visionHeight = m_vision_height;
                ActionObj.actionInfo.visionDepth = m_vision_depth;
                ActionObj.actionInfo.loadId = m_qr;
                ActionObj.actionInfo.loadWeight = m_drop_weight;

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

                if (m_Height_Distance_mm < 1300)
                {


                    ActionObj.actionInfo.shelf = false;

                }
                else
                {

                    Tools.Log($"@@shelf true", Tools.ELogType.BackEndLog);
                    ActionObj.actionInfo.shelf = true;
                }

                Tools.Log($"!####[drop Rack Event] {epc_data}", Tools.ELogType.BackEndLog);
                Tools.Log($"!#### LoadRate  : {ActionObj.actionInfo.loadRate}", Tools.ELogType.BackEndLog);
                Tools.Log($"!#### QR  : {m_qr}", Tools.ELogType.BackEndLog);
            

                Tools.Log($"##QR : {m_qr}", Tools.ELogType.BackEndLog);
                ActionObj.actionInfo.loadId = m_qr;


                

                string json_body = Util.ObjectToJson(ActionObj);
                RestClientPostModel post_obj = new RestClientPostModel();
    
                post_obj.body = json_body;
                post_obj.type = eMessageType.BackEndAction;
                Tools.Log($"##rftag epc  : {epc_data}", Tools.ELogType.BackEndLog);
                post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";

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
                Tools.Log($"Action : [drop] EPC  [{epc_data}] Rssi : [{rssi}] QR {m_qr} ", Tools.ELogType.ActionLog);


                if (ActionObj.actionInfo.shelf == true)
                {
                    DPS_EVENT2("2"); //대기 입고2 대기
                }

                SendToIndicator(0, 0, 0, "", 0, 0 , 0);
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

        private void Pattlite_Buzzer_LED(ePlayBuzzerLed  value)
        {
            if(value == ePlayBuzzerLed.ACTION_FAIL)
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
            else if(value == ePlayBuzzerLed.ACTION_FINISH)
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

            else if (value == ePlayBuzzerLed.QR_MEASRUE_OK)
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
            //Model.forklift_status.weightLeft = leftweight;
            //Model.forklift_status.weightRight = rightweight;
            Model.forklift_status.QR = QR;
            Model.forklift_status.visionWidth = vision_w;
            Model.forklift_status.visionHeight = vision_h;
            Model.forklift_status.visionDepth = vsion_depth;
            string json_body = Util.ObjectToJson(Model);
            _eventAggregator.GetEvent<IndicatorSendEvent>().Publish(json_body);
        }
    }
}
