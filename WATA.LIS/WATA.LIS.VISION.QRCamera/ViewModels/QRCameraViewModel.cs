using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using WATA.LIS.Core.Common;

namespace WATA.LIS.VISION.QRCamera.ViewModels
{
    public class QRCameraViewModel : BindableBase
    {
        public ObservableCollection<Log> ListQRCameraLog { get; set; }

        public QRCameraViewModel()
        {
            ListQRCameraLog = Tools.logInfo.ListQRCameraLog;
            Tools.Log($"Init QRCameraViewModel", Tools.ELogType.QRCameraLog);
        }
    }
}
