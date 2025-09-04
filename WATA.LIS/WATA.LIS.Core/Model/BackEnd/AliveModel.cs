using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class AliveModel
    {
        public Alive alive = new Alive();
    }

    public class Alive
    {
        public string workLocationId { get; set; }
        public string vehicleId { get; set; }
        public string projectId { get; set; }
        public string mappingId { get; set; }
        public string mapId { get; set; }
        public string errorCode { get; set; }
    }
}
