using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class VisionCamConfigModel : IQRCameraModel
    {
        public int vision_enable { get; set; }
        public string vision_name { get; set; }
        public string vision_ip { get; set; }
        public int vision_port { get; set; }
        public string vision_id { get; set; }
        public string vision_pw { get; set; }
    }
}
