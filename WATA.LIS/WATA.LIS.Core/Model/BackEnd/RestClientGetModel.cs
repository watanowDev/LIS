using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Core.Model.BackEnd
{
    public  class RestClientGetModel
    {
        public string url { get; set; }
        public string body { get; set; }
        public eMessageType type { get; set; }
    }
}


