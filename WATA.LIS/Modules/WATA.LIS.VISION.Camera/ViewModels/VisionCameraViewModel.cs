using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WATA.LIS.Core.Common;

namespace WATA.LIS.VISION.Camera.ViewModels
{
    public class VisionCameraViewModel : BindableBase
    {
        public ObservableCollection<Log> VisionLog { get; set; }
        public VisionCameraViewModel()
        {
            VisionLog = Tools.logInfo.ListVisionLog;
            Tools.Log($"Init VisionCameraViewModel", Tools.ELogType.VisionLog);

        }
    }
}
