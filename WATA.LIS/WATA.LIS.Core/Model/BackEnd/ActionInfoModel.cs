using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class ActionInfoModel
    {
        public actionInfo actionInfo = new actionInfo();
    }

    public class actionInfo
    {
        public string workLocationId { get; set; }
        public string action { get; set; }
        public string vehicleId { get; set; }
        public string epc { get; set; }
        public string height { get; set; }
        public string loadId { get; set; }
        public string loadRate { get; set; }
        public string loadMatrixRaw { get; set; }
        public string loadMatrixColumn { get; set; }
        public List<int> loadMatrix { get; set; } = new List<int>();
    }
}
