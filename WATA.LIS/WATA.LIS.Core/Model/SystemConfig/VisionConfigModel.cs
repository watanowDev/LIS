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
        public string CameraHeight { get; set; }
        public string QRValue { get; set; }
    }
}


