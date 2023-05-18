using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Events.RFID;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class GateEventModel
    {
        public gateEvent gateEvent = new gateEvent();
    }

    public class gateEvent
    {
        public string workLocationId { get; set; }
        public string vehicleId { get; set; }
        public string getLocation { get; set; }

        public string eventType { get;set; }
            

    }
}
