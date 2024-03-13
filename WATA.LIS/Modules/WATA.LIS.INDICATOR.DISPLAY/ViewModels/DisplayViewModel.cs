using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.Indicator;
using WATA.LIS.Core.Model.VISION;

namespace WATA.LIS.INDICATOR.DISPLAY.ViewModels
{
    public class DisplayViewModel : BindableBase
    {
        public ObservableCollection<Log> ListDisplayLog { get; set; }

        public DelegateCommand<string> ButtonFunc { get; set; }

        private readonly IEventAggregator _eventAggregator;

        public DisplayViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            ListDisplayLog = Tools.logInfo.ListDisplayLog;
            Tools.Log($"Init Display View  Model", Tools.ELogType.DisplayLog);
            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);
        }


        private void ButtonFuncClick(string command)
        {
            
            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "test":


                        SendToIndicator(77, 77, 77, "TEST", 88, 88);


                        break;


                    
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void SendToIndicator(int grossWeight, int leftweight, int rightweight, string QR, float vision_w, float vision_h)
        {
            IndicatorModel Model = new IndicatorModel();
            Model.forlift_status.weightTotal = grossWeight;
            Model.forlift_status.weightLeft = leftweight;
            Model.forlift_status.weightRight = rightweight;
            Model.forlift_status.QR = QR;
            Model.forlift_status.visionWidth = vision_w;
            Model.forlift_status.visionHeight = vision_h;

            string json_body = Util.ObjectToJson(Model);
            _eventAggregator.GetEvent<IndicatorSendEvent>().Publish(json_body);
        }
    }
}
