using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class BasicInfoModel
    {
        public int status { get; set; }
        public string message { get; set; }
        public object cause { get; set; }
        public object error { get; set; }
        public List<BasicInfoData> data { get; set; }
        public long timeStamp { get; set; }
    }

    public class BasicInfoData
    {
        public int pidx { get; set; }
        public int vidx { get; set; }
        public string workLocationId { get; set; }
        public string vehicleId { get; set; }
    }
}
