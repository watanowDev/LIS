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
using WATA.LIS.Core.Model.DPS;

namespace WATA.LIS.IF.DPS.ViewModels
{
    public class DPSViewModel : BindableBase
    {
        public ObservableCollection<Log> ListDPSLog { get; set; }

        public DelegateCommand<string> ButtonFunc { get; set; }

        private readonly IEventAggregator _eventAggregator;





        public DPSViewModel()
        {
            Tools.Log($"Init DPS Model", Tools.ELogType.DPSLog);
            ListDPSLog = Tools.logInfo.ListDPSLog;

            DPSAllClearModel model_byte = new DPSAllClearModel();
            model_byte.payload.AckType = 33;
            model_byte.payload.ControllerID = 44;
            model_byte.payload.LocationID = 55;
            byte[] target = Util.ObjectToByte(model_byte);

            string bytelog = Util.DebugBytestoString(target);

            Tools.Log($"test packet {bytelog}", Tools.ELogType.DPSLog);
        }
    }
}
