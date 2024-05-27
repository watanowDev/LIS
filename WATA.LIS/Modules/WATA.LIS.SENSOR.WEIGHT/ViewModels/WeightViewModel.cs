using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.WeightSensor;

namespace WATA.LIS.SENSOR.WEIGHT.ViewModels
{
    public class WeightViewModel : BindableBase
    {

        public ObservableCollection<Log> ListWeightLog { get; set; }
        public DelegateCommand<string> ButtonFunc { get; set; }

        private readonly IEventAggregator _eventAggregator;
        public WeightViewModel(IEventAggregator eventAggregator)
        {
           _eventAggregator = eventAggregator;
           ListWeightLog = Tools.logInfo.ListWeightLog;
           Tools.Log($"Init Weight View  Model", Tools.ELogType.WeightLog);
           ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);

        }

        private void ButtonFuncClick(string command)
        {
            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "ReadWeight":
                        Tools.Log($"Weight Request", Tools.ELogType.WeightLog);
                        byte[] ReadRueqst = { 0x55, 0xAB,0x01 ,0x00 };
                        _eventAggregator.GetEvent<WeightSensorSendEvent>().Publish(ReadRueqst);
                        break;

                    case "SendASCII":
                        Tools.Log($"ASCII Request", Tools.ELogType.WeightLog);
                        byte[] AsciiRequset = { 0x55, 0xab, 0x02, 0x96, 0x31, 0x32, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x32 };
                        _eventAggregator.GetEvent<WeightSensorSendEvent>().Publish(AsciiRequset);
                        break;

                    case "SendZeroSet":
                        Tools.Log($"ZeroSet Request", Tools.ELogType.WeightLog);
                        byte[] ZerosetReq = { 0x55, 0xAB, 0x03, 0x00 };
                        _eventAggregator.GetEvent<WeightSensorSendEvent>().Publish(ZerosetReq);
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
