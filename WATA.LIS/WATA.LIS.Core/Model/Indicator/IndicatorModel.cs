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
        public int weightTotal { get; set; }
        public int weightLeft { get; set; }
        public int weightRight { get; set; }
        public string QR { get; set; }
        public float visionHeight { get; set; }
        public float visionWidth { get; set; }
        public float visionDepth { get; set; }
        public string epc { get; set; } // epc

        public bool networkStatus { get; set; }
        public bool visionStauts { get; set; }
        public bool lidar2dStatus { get; set; }
        public bool lidar3dStatus { get; set; }
        public bool heightSensorStatus { get; set; }
        public bool rfidStatus { get; set; }

        public bool eventValue { get; set; } //true pickup false drop
        public bool is_unload { get; set; }
  
    }
}
 