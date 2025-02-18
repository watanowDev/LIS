using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Core.Model.StatusLED
{
    public class Patlite_Docker_LAMP_Model
    {
        public eLampAlert Lamp_Alert { get; set; }
        public eLampBuzzer Lamp_Buzzer { get; set; }
        public eLampColor Lamp_Color { get; set; }
        public eLampClear Lamp_Clear { get; set; }
        public eLampSequence Lamp_Sequence { get; set; }
    }
}
