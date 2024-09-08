using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.RIFID
{
    public class KeonnSensorModel
    {
        public string EPC { get; set; }
        public long TS { get; set; }
        public int PORT { get; set; }
        public int MUX1 { get; set; }
        public int MUX2 { get; set; }
        public int RSSI { get; set; }
        public int PHASE { get; set; }
        public int RC { get; set; }

    }
}
