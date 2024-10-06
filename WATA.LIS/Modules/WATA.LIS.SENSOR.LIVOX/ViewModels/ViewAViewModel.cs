using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.SENSOR.LIVOX.ViewModels
{
    public class ViewAViewModel : BindableBase
    {
        public ObservableCollection<Log> ListLIVOXLog { get; set; }

        public ViewAViewModel()
        {
            ListLIVOXLog = Tools.logInfo.ListLIVOXLog;
            Tools.Log($"Init ViewAViewModel", Tools.ELogType.LIVOXLog);

        }
    }
}
