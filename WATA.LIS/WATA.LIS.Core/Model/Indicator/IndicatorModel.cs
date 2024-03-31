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
        public forlift_status forlift_status = new forlift_status();
    }

    public class forlift_status
    {
        public int weightTotal { get; set; }
        public int weightLeft { get; set; }
        public int weightRight { get; set; }
        public string QR { get; set; }
        public float visionHeight { get; set; }
        public float visionWidth { get; set; }
        public float visionDepth { get; set; }
    }
}
 