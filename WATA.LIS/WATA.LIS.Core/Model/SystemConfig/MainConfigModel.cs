using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class MainConfigModel : IMainModel
    {
        public string forkLiftID { get; set; }   
        public string device_type { get; set; }


    }
}


