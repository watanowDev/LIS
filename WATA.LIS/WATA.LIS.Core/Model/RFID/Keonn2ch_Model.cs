using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.RFID
{
    public class Keonn2ch_Model
    {
        public bool CONNECTED { get; set; }
        public string EPC { get; set; }
        public int RSSI { get; set; }
        public int READCNT { get; set; }
        public DateTime TS { get; set; }

    }
}
