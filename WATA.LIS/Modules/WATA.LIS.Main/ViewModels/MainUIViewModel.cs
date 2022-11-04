using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Services;

namespace WATA.LIS.Main.ViewModels
{
    public class MainUIViewModel : BindableBase
    {

        public ObservableCollection<Log> ListSystemLog { get; set; }

        public MainUIViewModel(IStatusService mainStatusModel)
        {
            ListSystemLog = Tools.logInfo.ListSystemLog;
            Tools.Log($"Init MainUIViewModel", Tools.ELogType.SystemLog);
        }
    }
}
