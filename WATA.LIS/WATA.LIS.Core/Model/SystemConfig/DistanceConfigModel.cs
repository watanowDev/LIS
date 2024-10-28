using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class DistanceConfigModel : IDistanceModel
    {
        public int distance_enable { get; set; }
        public string model_name { get; set; }
        public string ComPort { get; set; }
        public int pick_up_distance_threshold { get; set; }
    }
}


