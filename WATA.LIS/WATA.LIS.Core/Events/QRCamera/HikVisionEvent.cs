using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Model.VisionCam;
using WATA.LIS.Core.Model.RFID;

namespace WATA.LIS.Core.Events.QRCamera
{
    public class HikVisionEvent : PubSubEvent<VisionCamModel>
    {

    }

    public class HikVisionEventTest : PubSubEvent<VisionCamModel>
    {

    }
}
