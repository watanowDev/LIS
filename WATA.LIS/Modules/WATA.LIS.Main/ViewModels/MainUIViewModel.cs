using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.VisionCam;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.VISION;
using WATA.LIS.Core.Services;
using System.Windows;
using System.IO;
using System.Windows.Media.Imaging;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.Main.ViewModels
{
    public class MainUIViewModel : BindableBase
    {
        public ObservableCollection<Log> ListSystemLog { get; set; }
        public ObservableCollection<Log> ListVisonCamLog { get; set; }
        public ObservableCollection<Log> ListBackEndLog { get; set; }
        public ObservableCollection<Log> ListWeightLog { get; set; }
        public ObservableCollection<Log> ListDistancetLog { get; set; }
        public ObservableCollection<Log> ListRFIDLog { get; set; }
        public ObservableCollection<Log> ListIndicatortLog { get; set; }
        public ObservableCollection<Log> ListActionLog { get; set; }


        // Config 데이터 클래스
        private DistanceConfigModel m_distanceConfig;


        //CamStream
        private BitmapImage _currentFrame;
        public BitmapImage CurrentFrame { get { return _currentFrame; } set { SetProperty(ref _currentFrame, value); } }


        //Elips
        //private string _Weight_Active;
        //public string Weight_Active { get { return _Weight_Active; } set { SetProperty(ref _Weight_Active, value); } }


        //private string _Distance_Active;
        //public string Distance_Active { get { return _Distance_Active; } set { SetProperty(ref _Distance_Active, value); } }


        //private string _RFID_Active;
        //public string RFID_Active { get { return _RFID_Active; } set { SetProperty(ref _RFID_Active, value); } }


        //private string _VISION_Active;
        //public string VISIONCAM_Active { get { return _VISION_Active; } set { SetProperty(ref _VISION_Active, value); } }


        //private string _BACKEND_Active;
        //public string BACKEND_Active { get { return _BACKEND_Active; } set { SetProperty(ref _BACKEND_Active, value); } }


        //Text
        private string _Weight_Value;
        public string Weight_Value { get { return _Weight_Value; } set { SetProperty(ref _Weight_Value, value); } }


        private string _RightBattery_Value;
        public string RightBattery_Value { get { return _RightBattery_Value; } set { SetProperty(ref _RightBattery_Value, value); } }


        private string _LeftBattery_Value;
        public string LeftBattery_Value { get { return _LeftBattery_Value; } set { SetProperty(ref _LeftBattery_Value, value); } }


        private string _Distance_Value;
        public string Distance_Value { get { return _Distance_Value; } set { SetProperty(ref _Distance_Value, value); } }


        private string _RFID_Value;
        public string RFID_Value { get { return _RFID_Value; } set { SetProperty(ref _RFID_Value, value); } }


        private string _BACKEND_Value;
        public string BACKEND_Value { get { return _BACKEND_Value; } set { SetProperty(ref _BACKEND_Value, value); } }


        private string _DEPTH_Value;
        public string Depth_Value { get { return _DEPTH_Value; } set { SetProperty(ref _DEPTH_Value, value); } }

        private string _TM_DEPTH;
        public string TM_DEPTH{ get { return _TM_DEPTH; } set { SetProperty(ref _TM_DEPTH, value); } }

        private string _ML_DEPTH;
        public string ML_DEPTH{ get { return _ML_DEPTH; } set { SetProperty(ref _ML_DEPTH, value); } }

        private string _MM_DEPTH;
        public string MM_DEPTH{ get { return _MM_DEPTH; } set { SetProperty(ref _MM_DEPTH, value); } }

        private string _MR_DEPTH;
        public string MR_DEPTH{ get { return _MR_DEPTH; } set { SetProperty(ref _MR_DEPTH, value); } }

        private string _BL_DEPTH;
        public string BL_DEPTH{ get { return _BL_DEPTH; } set { SetProperty(ref _BL_DEPTH, value); } }

        private string _BM_DEPTH;
        public string BM_DEPTH{ get { return _BM_DEPTH; } set { SetProperty(ref _BM_DEPTH, value); } }

        private string _BR_DEPTH;
        public string BR_DEPTH{ get { return _BR_DEPTH; } set { SetProperty(ref _BR_DEPTH, value); } }


        //Image
        private VisionCamModel _VisionCam_Frame;
        public VisionCamModel VisionCam_Frame { get { return _VisionCam_Frame; } set { SetProperty(ref _VisionCam_Frame, value); } }


        private string Active = "#FF5DF705";//light Green color
        private string Disable = "DimGray";
        private string Disconnect = "Red";

        IEventAggregator _eventAggregator;
        public MainUIViewModel(IStatusService mainStatusModel, IEventAggregator eventAggregator, IDistanceModel distanceModel)
        {
            _eventAggregator = eventAggregator;
            ListSystemLog = Tools.logInfo.ListSystemLog;
            ListVisonCamLog = Tools.logInfo.ListVisionCamLog;
            ListBackEndLog = Tools.logInfo.ListBackEndLog;
            ListWeightLog = Tools.logInfo.ListWeightLog;
            ListDistancetLog = Tools.logInfo.ListDistanceLog;
            ListRFIDLog = Tools.logInfo.ListRFIDLog;
            ListIndicatortLog = Tools.logInfo.ListDisplayLog;
            ListActionLog = Tools.logInfo.ListActionLog;

            m_distanceConfig = (DistanceConfigModel)distanceModel;

            Tools.Log($"Init MainUIViewModel", Tools.ELogType.SystemLog);
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.VisionCamLog);
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.BackEndLog);
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.WeightLog);
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.DistanceLog);
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.RFIDLog);
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.DisplayLog);
            //Tools.Log($"Init MainUIViewModel", Tools.ELogType.VisionLog);

            _eventAggregator.GetEvent<WeightSensorEvent>().Subscribe(OnWeightSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RackProcess_Event>().Subscribe(OnRFIDSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<HikVisionEvent>().Subscribe(OnVisionCamStreaming, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<BackEndStatusEvent>().Subscribe(OnBackEndStatus, ThreadOption.BackgroundThread, true);


            //Weight_Active = Disable;
            //Distance_Active = Disable;
            //RFID_Active = Disable;
            //VISIONCAM_Active = Disable;
            //BACKEND_Active = Disable;
        }

        /// <summary>
        /// WeightSensorData
        /// </summary>
        /// <param name="model"></param>
        private void OnWeightSensorData(WeightSensorModel model)
        {
            if (model != null || model.LeftOnline != true || model.RightOnline != true)
            {
                //Weight_Active = Active;
                LeftBattery_Value = model.LeftBattery.ToString();
                RightBattery_Value = model.RightBattery.ToString();
                Weight_Value = model.GrossWeight.ToString();
            }
            else
            {
                //Weight_Active = Disconnect;
            }
        }

        /// <summary>
        /// VisionCamStream
        /// </summary>
        /// <param name="model"></param>
        private void OnVisionCamStreaming(VisionCamModel model)
        {
            if (model?.connected == true)
            {
                // UI 스레드에서 CurrentFrame 속성을 업데이트
                Application.Current.Dispatcher.Invoke(() => {
                    CurrentFrame = ConvertToBitmapImage(model.FRAME);
                    Depth_Value = model.ACTION_DEPTH.ToString() + "mm";
                    TM_DEPTH= model.TM_DEPTH.ToString("F0") + "mm";
                    ML_DEPTH= model.ML_DEPTH.ToString("F0") + "mm";
                    MM_DEPTH= model.MM_DEPTH.ToString("F0") + "mm";
                    MR_DEPTH= model.MR_DEPTH.ToString("F0") + "mm";
                    BL_DEPTH= model.BL_DEPTH.ToString("F0") + "mm";
                    BM_DEPTH= model.BM_DEPTH.ToString("F0") + "mm";
                    BR_DEPTH= model.BR_DEPTH.ToString("F0") + "mm";
                    //VISIONCAM_Active = Active;
                });
            }
        }

        private BitmapImage ConvertToBitmapImage(byte[] frame)
        {
            using (var stream = new MemoryStream(frame))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // BitmapImage를 다른 스레드에서 사용할 수 있도록 고정
                return bitmap;
            };
        }


        /// <summary>
        /// BackEndLog
        /// </summary>
        /// <param name="status"></param>
        public void OnBackEndStatus(int status)
        {
            if (status == -1)
            {
                //BACKEND_Active = Disconnect;
                GlobalValue.IS_ERROR.backend = false;
            }
            else
            {
                //BACKEND_Active = Active;
                GlobalValue.IS_ERROR.backend = true;
            }
        }

        /// <summary>
        /// DistanceLog
        /// </summary>
        /// <param name="obj"></param>
        public void OnDistanceSensorData(DistanceSensorModel obj)
        {
            if (obj.connected == false)
            {
                //Distance_Active = Disconnect;
            }
            else if (obj.connected == true)
            {
                //Distance_Active = Active;
            }

            Distance_Value = $"RAW:{obj.Distance_mm - 60}mm, {(obj.Distance_mm - 60 - m_distanceConfig.pick_up_distance_threshold)}mm";
        }

        public void OnRFIDSensorData(RackRFIDEventModel obj)
        {
            if (obj.EPC == "NA")
            {
                //RFID_Active = Disable;
            }
            else
            {

                //RFID_Active = Active;
            }


            RFID_Value = obj.EPC;
        }
    }
}