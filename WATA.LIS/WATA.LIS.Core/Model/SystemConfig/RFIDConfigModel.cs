using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class RFIDConfigModel : IRFIDModel
    {
        public string SPP_MAC { get; set; }
        public int nRadioPower { get; set; }
        
        public int nTxOnTime { get; set; }
        public int nTxOffTime { get; set; }

        public int nToggle { get; set; }
        public int nSpeakerEnable { get; set; }
    }
}


