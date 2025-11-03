using Prism.Commands;
using Prism.Events;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.LIVOX;
using WATA.LIS.Core.Events.NAVSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.LIVOX;
using WATA.LIS.Core.Model.NAV;
using WATA.LIS.Core.Model.VISION;
using WATA.LIS.Core.Model.VisionCam;

namespace WATA.LIS.Main.ViewModels
{
    public class TopBarUIViewModel : BindableBase
    {


        private string _VersionContext;
        public string VersionContext { get { return _VersionContext; } set { SetProperty(ref _VersionContext, value); } }


        private string _Date;
        public string Date { get { return _Date; } set { SetProperty(ref _Date, value); } }


        private string _Time;
        public string Time { get { return _Time; } set { SetProperty(ref _Time, value); } }


        private string _LivingTime;
        public string LivingTime { get { return _LivingTime; } set { SetProperty(ref _LivingTime, value); } }


        private string _VisionCamQREvent;
        public string VisionCamQREvent { get { return _VisionCamQREvent; } set { SetProperty(ref _VisionCamQREvent, value); } }


        private string _VisionCamQRListEvent;
        public string VisionCamQRListEvent { get { return _VisionCamQRListEvent; } set { SetProperty(ref _VisionCamQRListEvent, value); } }


        private string _VisionCamSizeEvent;
        public string VisionCamSizeEvent { get { return _VisionCamSizeEvent; } set { SetProperty(ref _VisionCamSizeEvent, value); } }


        private string _RFIDEvent;
        public string RFIDEvent { get { return _RFIDEvent; } set { SetProperty(ref _RFIDEvent, value); } }


        private string _CoordinatesEvent;
        public string CoordinatesEvent { get { return _CoordinatesEvent; } set { SetProperty(ref _CoordinatesEvent, value); } }

        public DelegateCommand<string> ButtonFunc { get; set; }
        IEventAggregator _eventAggregator;

        // 프로세스 시작 시각을 기준으로 업타임 계산
        private readonly DateTime _processStartLocal = Process.GetCurrentProcess().StartTime;

        public TopBarUIViewModel(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            VersionContext = "(LIS)" + GlobalValue.SystemVersion;

            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<HittingQR_Event>().Subscribe(OnCurrentQREvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<HittingQrList_Event>().Subscribe(OnCurrentQrListEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<LIVOXEvent>().Subscribe(OnCurrentSizeEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<HittingEPC_Event>().Subscribe(OnCurrentRFIDEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<CoordinatesEvent>().Subscribe(OnNavSensorEvent, ThreadOption.BackgroundThread, true);

            DispatcherTimer DateTimer = new DispatcherTimer();
            DateTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            DateTimer.Tick += new EventHandler(Timer);
            DateTimer.Start();


            VisionCamQREvent = "None";
            VisionCamSizeEvent = "None";
            RFIDEvent = "None";
        }

        private void OnNavSensorEvent(CoordinatesModel model)
        {
            if (model == null) return;
            if (model.status != "1") return;
            if (model.action != "pickdrop") return;

            CoordinatesEvent = $"X: {model.naviX}, Y: {model.naviY}, T: {model.naviT}, Action: {model.action}";
        }

        private void OnCurrentSizeEvent(LIVOXModel obj)
        {
            VisionCamSizeEvent = $"Width: {obj.width / 10}cm, Height: {obj.height / 10}cm, Depth: {obj.length / 10}cm";
        }

        private void OnCurrentRFIDEvent(string obj)
        {
            RFIDEvent = obj;
        }

        private void OnCurrentQREvent(string obj)
        {
            VisionCamQREvent = obj;
        }

        private void OnCurrentQrListEvent(List<string> list)
        {
            VisionCamQRListEvent = string.Join(", ", list);
        }

        private static int LivingCount = 0;

        private void Timer(object sender, EventArgs e)
        {
            Date = DateTime.Now.ToString("yyyy-MM-dd");
            Time = DateTime.Now.ToString("HH:mm:ss");

            // 앱 가동 경과(업타임) HH:mm:ss 표시
            try
            {
                var uptime = DateTime.Now - _processStartLocal;
                if (uptime < TimeSpan.Zero) uptime = TimeSpan.Zero;
                LivingTime = uptime.ToString(@"hh\:mm\:ss");
            }
            catch
            {
                // Fallback: 단순 카운트
                LivingTime = (LivingCount++).ToString();
            }
        }

        private void ButtonFuncClick(string command)
        {
            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "Exit":

                        if (MessageBox.Show(" Do you want to exit the program? ", "Program", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
                        {
                            ClearProcess();
                            Environment.Exit(0);
                        }
                        break;

                    case "Stop":
                        {
                            //_eventAggregator.GetEvent<SimulModeEvent>().Publish(obj);

                            break;
                        }

                    case "r1":
                        {
                            SimulationModel obj = new SimulationModel();
                            obj.EPC = "DA00025C00020000000100ED";
                            _eventAggregator.GetEvent<SimulModeEvent>().Publish(obj);

                            break;
                        }


                    case "r2":
                        {
                            SimulationModel obj = new SimulationModel();
                            obj.EPC = "DA00025C00020000000200ED";
                            _eventAggregator.GetEvent<SimulModeEvent>().Publish(obj);

                            break;
                        }

                    case "r3":
                        {
                            SimulationModel obj = new SimulationModel();
                            obj.EPC = "DA00025C00020000000300ED";
                            _eventAggregator.GetEvent<SimulModeEvent>().Publish(obj);
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

        private void ClearProcess()
        {
            Process[] processList = Process.GetProcessesByName("WATA.LIS.WPS");
            for (int i = processList.Length - 1; i >= 0; i--)
            {
                // processList[i].CloseMainWindow();
                processList[i].Kill();
                processList[i].Close();
            }

            Process[] processList_vision = Process.GetProcessesByName("vision_forklift");
            for (int i = processList_vision.Length - 1; i >= 0; i--)
            {
                // processList[i].CloseMainWindow();
                processList_vision[i].Kill();
                processList_vision[i].Close();
            }
        }
    }
}
