using NetMQ;
using NetMQ.Sockets;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Model.RFID;

namespace WATA.LIS.VISION.Camera.ViewModels
{
    public class VisionCameraViewModel : BindableBase
    {
        Thread RecvThread;
        public ObservableCollection<Log> VisionLog { get; set; }
        public VisionCameraViewModel()
        {
            VisionLog = Tools.logInfo.ListVisionLog;
            Tools.Log($"Init VisionCameraViewModel", Tools.ELogType.VisionLog);

            

        }



    }
}
