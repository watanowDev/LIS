using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.RFID
{
    public class LocationRFIDEventModel
    {
        public string EPC { get; set; }
        public float RSSI { get; set; }
    }
}
