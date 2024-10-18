using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.VISION
{
    public class WeightSensorModel
    {
        public int GrossWeight { get; set; }
        public int RightWeight { get; set; }
        public int LeftWeight { get; set; }
        public int RightBattery { get; set; }
        public int LeftBattery { get; set; }
        public bool RightIsCharging { get; set; }
        public bool leftIsCharging { get; set; }
        public bool RightOnline { get; set; }
        public bool LeftOnline { get; set; }
        public bool GrossNet { get; set; }
        public bool OverLoad { get; set; }
        public bool OutOfTolerance { get; set; }
    }
}


