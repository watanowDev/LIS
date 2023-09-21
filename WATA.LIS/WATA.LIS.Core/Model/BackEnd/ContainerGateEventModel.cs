using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class ContainerGateEventModel
    {
        public string epc { get; set; }
        public string loadId { get; set; }
        public string vehicleId { get; set; }
    }
}
