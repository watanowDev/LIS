using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.RFID
{
    public class Keonn2chEventModel
    {
        public string EPC { get; set; }
        public DateTime TS { get; set; }
        public int RSSI { get; set; }
        public int COUNT { get; set; }

    }
}
