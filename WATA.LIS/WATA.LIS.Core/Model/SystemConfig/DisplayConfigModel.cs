using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class DisplayConfigModel : IDisplayModel
    {
        public int display_enable { get; set; }
        public string display_type { get; set; }
    }
}
