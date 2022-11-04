using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WATA.LIS.Core.Common;

namespace WATA.LIS.IF.BE.ViewModels
{
    public class BackEndViewModel : BindableBase
    {
        public ObservableCollection<Log> ListBackEndLog { get; set; }

        public BackEndViewModel()
        {
            ListBackEndLog = Tools.logInfo.ListBackEndLog;
            Tools.Log($"Init BackEndViewModel", Tools.ELogType.BackEndLog);

        }
    }
}
