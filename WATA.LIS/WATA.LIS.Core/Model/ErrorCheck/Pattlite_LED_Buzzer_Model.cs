using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Core.Model.ErrorCheck
{
    public class Pattlite_LED_Buzzer_Model
    {
        public eLEDColors LED_Color { get; set; }
        public eLEDPatterns LED_Pattern { get; set; }
        public eBuzzerPatterns BuzzerPattern { get; set; }
        public ePlayInfoSpeaker PlayInfoSpeaker { get; set; }
        public int BuzzerCount { get; set; }
    }
}


