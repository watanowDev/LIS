using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class AliveModel
    {
        public Alive alive = new Alive();
    }

    public class Alive
    {
        public string Work_Location_ID { get; set; }
        public string Vehicle_ID { get; set; }
        public string ErrorCode { get; set; }
    }
}
