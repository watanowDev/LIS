using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.NAV
{
    public class NAVSensorModel
    {
        public long naviX { get; set; }
        public long naviY { get; set; }
        public long naviT { get; set; }
        public string zoneId { get; set; }
        public string zoneName { get; set; }
        public string projectId { get; set; }
        public string mappingId { get; set; }
        public string mapId { get; set; }
        public string result { get; set; }
        public string vehicleId { get; set; }
    }
}
