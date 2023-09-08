using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class WeightConfigModel : IWeightModel
    {
        public string ComPort { get; set; }
        public int loadweight_timeout { get; set; }

        public string sensor_value { get; set; }
    }
}


