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
        public containerInfo containerInfo = new containerInfo();
    }

    public class containerInfo
    {
        public string epc { get; set; }
        public string loadId { get; set; }
        public string vehicleId { get; set; }
        public string projectId { get; set; }
        public string mappingId { get; set; }
        public string mapId { get; set; }
    }
}

