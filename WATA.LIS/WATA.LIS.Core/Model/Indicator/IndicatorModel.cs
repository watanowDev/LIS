using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Core.Model.Indicator
{
    public class IndicatorModel
    {
        public forklift_status forklift_status = new forklift_status();
    }

    public class forklift_status
    {
        public int command { get; set; } // 0. alive, 1. loaded, 2. 상차요청응답, 3. 하차요청응답, 4. 상차중드롭이벤트, 5. 하차중드롭이벤트

        public string QR { get; set; }
        public int weightTotal { get; set; }
        public float visionHeight { get; set; }
        public float visionWidth { get; set; }
        public float visionDepth { get; set; }
        public string epc { get; set; }

        public bool networkStatus { get; set; } // 외부망 연결 상태
        public bool visionCamStauts { get; set; }
        public bool lidar2dStatus { get; set; }
        public bool lidar3dStatus { get; set; }
        public bool heightSensorStatus { get; set; }
        public bool rfidStatus { get; set; }
    }
}
