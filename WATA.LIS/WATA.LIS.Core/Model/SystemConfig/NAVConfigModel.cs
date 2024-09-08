using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class NAVConfigModel : INAVModel
    {
        public int NAV_Enable { get; set; }
        public string IP { get; set; }
        public int PORT { get; set; }
    }
}
