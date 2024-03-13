using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.ErrorCheck
{
    public class DPSAllClear
    {
        public bool camera { get; set; }
        public bool distance { get; set; }
        public bool backend { get; set; }
        public bool rfid { get; set; }
    }
}


