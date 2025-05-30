using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.VisionCam
{
    public class V2DetectionModel
    {
        public int ClassId { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float W { get; set; }
        public float H { get; set; }
        public float Confidence { get; set; }
    }
}
