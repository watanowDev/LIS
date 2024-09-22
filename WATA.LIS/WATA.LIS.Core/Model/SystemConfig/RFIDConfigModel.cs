using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class RFIDConfigModel : IRFIDModel
    {
        public int rfid_enable { get; set; }
        public string rfid_name { get; set; }
        public string comport {  get; set; }
        public string SPP_MAC { get; set; }
        public int nRadioPower { get; set; }
        
        public int nTxOnTime { get; set; }
        public int nTxOffTime { get; set; }

        public int nToggle { get; set; }
        public int nSpeakerlevel { get; set; }

        public int nRssi_pickup_timeout { get; set; }

        public int nRssi_pickup_threshold { get; set; }


        public int nRssi_drop_timeout { get; set; }

        public int nRssi_drop_threshold { get; set; }

        public string front_ant_port;

        public string ip { get; set; }


    }
}


