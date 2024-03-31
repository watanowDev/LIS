using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class Led_Buzzer_ConfigModel : ILedBuzzertModel
    {
        public int volume { get; set; }
        public string InfoLanguage { get; set; }
    }
}


