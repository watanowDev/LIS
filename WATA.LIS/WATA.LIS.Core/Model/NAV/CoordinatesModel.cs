using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.NAV
{
    public class CoordinatesModel
    {
        public long naviX { get; set; }
        public long naviY { get; set; }
        public long naviT { get; set; }
        public string status { get; set; } = string.Empty;
        public string action { get; set; } = string.Empty;
    }
}
