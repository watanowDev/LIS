using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WATA.LIS.Core.Common;

namespace WATA.LIS.SENSOR.UHF_RFID.ViewModels
{
    public class UHFRfidViewModel : BindableBase
    {
        public ObservableCollection<Log> ListRFIDLog { get; set; }
        public UHFRfidViewModel()
        {
            ListRFIDLog = Tools.logInfo.ListRFIDLog;
            //Tools.Log($"Init UHFRfidViewModel", Tools.ELogType.RFIDLog);
                
        }
    }
}
