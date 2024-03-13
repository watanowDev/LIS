using MaterialDesignThemes.Wpf;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.DPS;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.VISION;
using Windows.Services.Maps;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.Core.Services
{
    /*
     * StatusService_GateChecker 구성요소
     * DPS 바란 솔루션 
     */

    public class StatusService_DPS : IStatusService
    {
        IEventAggregator _eventAggregator;

        public StatusService_DPS(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            // _eventAggregator.GetEvent<Gate_Event>().Subscribe(OnGateData, ThreadOption.BackgroundThread, true);
            Tools.Log($"StatusService_DPS", Tools.ELogType.SystemLog);

            _eventAggregator.GetEvent<DPSRecvEvent>().Subscribe(OnReceive, ThreadOption.BackgroundThread, true);
        }
        public void OnReceive(byte[] RecvBuffer)
        {
            if (RecvBuffer[0] == 0x45)
            {
                Tools.Log($"Alive", Tools.ELogType.DPSLog);
            }
            else if (RecvBuffer[0] == 0x65)
            {
                Tools.Log($"Button Event", Tools.ELogType.DPSLog);
            }
        }
    }
}
