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
using WATA.LIS.SENSOR.NAV.NAV;

namespace WATA.LIS.SENSOR.NAV.ViewModels
{
    public class NAVViewModel : BindableBase
    {
        public ObservableCollection<Log> NAVLog { get; set; }
        private readonly IEventAggregator _eventAggregator;
        public DelegateCommand<string> ButtonFunc { get; set; }

        public NAVViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            NAVLog = Tools.logInfo.ListNAVLog;

            _eventAggregator.GetEvent<IndicatorRecvEvent>().Subscribe(OnIndicatorEvent, ThreadOption.BackgroundThread, true);
        }

        public void OnIndicatorEvent(string status)
        {
            Tools.Log($"OnIndicatorEvent {status}", Tools.ELogType.NAVLog);

        }

        


    }
}
