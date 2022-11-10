using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class ActionInfoModel
    {
        public ActionInfo ActionInfo = new ActionInfo();
    }

    public class ActionInfo
    {
        public string Work_Location_ID { get; set; }
        public string Action { get; set; }
        public string Vehicle_ID { get; set; }
        public string EPC { get; set; }
        public string Height { get; set; }
        public string Load_Rate { get; set; }
        public string Load_Matrix_Raw { get; set; }
        public string Load_Matrix_Column { get; set; }
        byte[] Load_Matrix { get; set; }
    }
}
