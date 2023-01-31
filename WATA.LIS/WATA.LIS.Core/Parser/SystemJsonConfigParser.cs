using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Model.RFID;
using Windows.Security.Cryptography.Core;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.Core.Parser
{
    public  class SystemJsonConfigParser
    {
        public (DistanceConfigModel, VisionConfigModel, RFIDConfigModel, MainConfigModel) LoadJsonfile()
        {

            DistanceConfigModel distance = new DistanceConfigModel();
            VisionConfigModel vision = new VisionConfigModel();
            MainConfigModel main = new MainConfigModel();
            RFIDConfigModel rfid = new RFIDConfigModel();

            try
            {
                string path = System.IO.Directory.GetCurrentDirectory() + "\\SystemConfig\\SystemConfig.json";
                using (StreamReader file = File.OpenText(path))
                {
                    using (JsonTextReader reader = new JsonTextReader(file))
                    {
                        JObject json = (JObject)JToken.ReadFrom(reader);
                        main.forkLiftID = json["main"]["fork_lift_id"].ToString();
                        distance.ComPort  = json["distancesensor"]["comport"].ToString();
                        vision.CameraHeight = json["visioncamera"]["camera_height"].ToString();
                        vision.QRValue = json["visioncamera"]["qr_enable"].ToString();

                        rfid.nRadioPower = (int)json["rfid_receiver"]["radio_power"];
                        rfid.nTxOnTime = (int)json["rfid_receiver"]["tx_on_time"];
                        rfid.nTxOffTime = (int)json["rfid_receiver"]["tx_off_time"];
                        rfid.nToggle = (int)json["rfid_receiver"]["toggle"];
                        rfid.nSpeakerEnable = (int)json["rfid_receiver"]["speaker_enable"];
                        rfid.SPP_MAC = json["rfid_receiver"]["SPP_MAC"].ToString();
                        Tools.Log($"Load SystemConfig {json.ToString()}", Tools.ELogType.SystemLog);
                    } 
                }
            }
            catch (Exception Ex)
            {
                Tools.Log($"Exception !!!", Tools.ELogType.DistanceLog);
            }

            return (distance, vision, rfid, main);
        }
    }
}
