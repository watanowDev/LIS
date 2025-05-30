using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;
using Windows.Media.AppBroadcasting;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class Led_Buzzer_ConfigModel : ILedBuzzertModel
    {
        public int led_enable { get; set; }
        public int OnlySpeark { get; set; }
        public int volume { get; set; }
        public string lamp_IP { get; set; }
        public string InfoLanguage { get; set; }
    }
}


