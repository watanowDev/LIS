using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.VISION.QRCamera.ViewModels
{
    public class QRCameraViewModel : BindableBase
    {
        ObservableCollection<Log> ListQRCameraLog { get; set; }

        public QRCameraViewModel()
        {
            ListQRCameraLog = Tools.logInfo.ListQRCameraLog;
            Tools.Log($"Init QRCameraViewModel", Tools.ELogType.QRCameraLog);
        }
    }
}
