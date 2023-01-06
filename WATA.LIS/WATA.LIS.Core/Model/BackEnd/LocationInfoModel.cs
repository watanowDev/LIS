using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace WATA.LIS.Core.Model.BackEnd
{
    public class LocationInfoModel
    {
        public locationInfo locationInfo = new locationInfo();
    }

    public class locationInfo
    {
        public string workLocationId { get; set; }
        public string vehicleId { get; set; }
        public string epc { get; set; }
    }
}
