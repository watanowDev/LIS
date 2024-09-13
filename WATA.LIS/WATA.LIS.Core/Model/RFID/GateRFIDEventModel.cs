using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.RFID
{
    public class GateRFIDEventModel
    {
        public string GateValue { get; set; }
        public string EPC { get; set; }
        public float RSSI { get; set; }
    }
}


