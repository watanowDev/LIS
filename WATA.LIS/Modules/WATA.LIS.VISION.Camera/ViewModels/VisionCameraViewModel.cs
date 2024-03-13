using NetMQ;
using NetMQ.Sockets;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.VISION;

namespace WATA.LIS.VISION.Camera.ViewModels
{
    public class VisionCameraViewModel : BindableBase
    {
        Thread RecvThread;
        public ObservableCollection<Log> VisionLog { get; set; }
        private readonly IEventAggregator _eventAggregator;
        public DelegateCommand<string> ButtonFunc { get; set; }
        public VisionCameraViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            VisionLog = Tools.logInfo.ListVisionLog;
            Tools.Log($"Init VisionCameraViewModel", Tools.ELogType.VisionLog);


            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);


            _eventAggregator.GetEvent<IndicatorRecvEvent>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);


        }



        public void OnIndicatorEvent(string status)
        {
            Tools.Log($"OnIndicatorEvent {status}", Tools.ELogType.DisplayLog);

            if (status == "pick_up")
            {
           //     Pattlite_Buzzer_LED(ePlayBuzzerLed.MEASRUE_OK);
            }
        }


        private byte[] m_LoadMatrix = new byte[10];

        private void ButtonFuncClick(string command)
        {
            VISON_Model visionModel = new VISON_Model();

            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "pickup":


                        visionModel.area = (float)0.88;
                        visionModel.status = "pickup";
                        visionModel.width =  (float)0.88;
                        visionModel.height = (int)0.88;
                        visionModel.qr = "test";
                        visionModel.matrix = m_LoadMatrix;

                        _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel);
                        break;


                    case "drop":

                        visionModel.area = (float)0.88;
                        visionModel.status = "drop";
                        visionModel.width = (float)0.88;
                        visionModel.height = (int)0.88;
                        visionModel.qr = "test";

                        visionModel.matrix = m_LoadMatrix;

                        _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel);
                        break;



                    default:
                        break;
                }
            }
            catch (Exception ex)
            {

            }
        }



    }
}
