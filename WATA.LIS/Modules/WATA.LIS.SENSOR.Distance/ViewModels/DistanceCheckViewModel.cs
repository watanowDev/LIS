using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WATA.LIS.Core.Common;


namespace WATA.LIS.SENSOR.Distance.ViewModels
{
    public class DistanceCheckViewModel : BindableBase
    {
        

        public ObservableCollection<Log> ListDistanceLog { get; set; }

        public DistanceCheckViewModel()
        {
            ListDistanceLog = Tools.logInfo.ListDistanceLog;
            Tools.Log($"Init DistanceCheckViewModel", Tools.ELogType.DistanceLog);
        
        }
    }
}
