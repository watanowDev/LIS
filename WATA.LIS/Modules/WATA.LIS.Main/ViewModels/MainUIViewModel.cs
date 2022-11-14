using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.VISION;
using WATA.LIS.Core.Services;

namespace WATA.LIS.Main.ViewModels
{
    public class MainUIViewModel : BindableBase
    {
        public ObservableCollection<Log> ListSystemLog { get; set; }

        //Elips
        private string _Distance_Active;
        public string Distance_Active { get { return _Distance_Active; } set { SetProperty(ref _Distance_Active, value); } }
        private string _RFID_Active;
        public string RFID_Active { get { return _RFID_Active; } set { SetProperty(ref _RFID_Active, value); } }
        private string _VISION_Active;
        public string VISION_Active { get { return _VISION_Active; } set { SetProperty(ref _VISION_Active, value); } }

        private string _BACKEND_Active;
        public string BACKEND_Active { get { return _BACKEND_Active; } set { SetProperty(ref _BACKEND_Active, value); } }

        //Text
        private string _Distance_Value;
        public string Distance_Value { get { return _Distance_Value; } set { SetProperty(ref _Distance_Value, value); } }

        private string _RFID_Value;
        public string RFID_Value { get { return _RFID_Value; } set { SetProperty(ref _RFID_Value, value); } }

        private string _VISION_Value;
        public string VISION_Value { get { return _VISION_Value; } set { SetProperty(ref _VISION_Value, value); } }


        private string _BACKEND_Value;
        public string BACKEND_Value { get { return _BACKEND_Value; } set { SetProperty(ref _BACKEND_Value, value); } }

        private string Active = "#FF5DF705";//light Green color
        private string Disable = "DimGray";
        private string Disconnect = "Red";

        IEventAggregator _eventAggregator;
        public MainUIViewModel(IStatusService mainStatusModel, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            ListSystemLog = Tools.logInfo.ListSystemLog;
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.SystemLog);
            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RFIDSensorEvent>().Subscribe(OnRFIDSensorData, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<VISION_Event>().Subscribe(OnVISIONEvent, ThreadOption.BackgroundThread, true);
            Distance_Active = Disable;
            RFID_Active = Disable;
            VISION_Active = Disable;
            BACKEND_Active = Disable;
        }

        public void OnDistanceSensorData(DistanceSensorModel obj)
        {
           if (obj.Distance_mm == -100)
           {
                Distance_Active = Disconnect;
           }
           else
           {
                Distance_Active = Active;
           }

            Distance_Value = obj.Distance_mm.ToString();
        }

        public void OnRFIDSensorData(RFIDSensorModel obj)
        {
            if(obj.EPC_Data == "NA")
            {
                RFID_Active = Disable;
            }
            else
            {

                RFID_Active = Active;
            }


            RFID_Value = obj.EPC_Data;
        }

        public void OnVISIONEvent(VISON_Model obj)
        {
            Tools.Log("OnVISIONEvent {}", Tools.ELogType.SystemLog);
        }
    }
}
