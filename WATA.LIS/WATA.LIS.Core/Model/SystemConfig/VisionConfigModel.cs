using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class VisionConfigModel : IVisionModel
    {
        public int vision_enable { get; set; }

        public double CameraHeight { get; set; }
        public int QRValue { get; set; }

        public int view_3d_enable { get; set; }

        public float event_distance { get; set; }

        public int pickup_wait_delay { get; set; }

        public float rack_with { get; set; }

        public float rack_height { get; set; }
    }
}


