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
                        main.forkLiftID = json["main"]["unit_id"].ToString();
                        main.device_type = json["main"]["device_type"].ToString();
                        distance.ComPort  = json["distancesensor"]["comport"].ToString();

                        vision.CameraHeight = (float)json["visioncamera"]["camera_height"];
                        vision.QRValue = (int)json["visioncamera"]["qr_enable"];
                        vision.view_3d_enable = (int)json["visioncamera"]["view_3d_enable"];
                        vision.event_distance = (float)json["visioncamera"]["event_distance"];

                        vision.pickup_wait_delay = (int)json["visioncamera"]["pickup_wait_delay"];
                        vision.rack_with = (float)json["visioncamera"]["rack_with"];
                        vision.rack_height = (float)json["visioncamera"]["rack_height"];



                        rfid.nRadioPower = (int)json["rfid_receiver"]["radio_power"];
                        rfid.nTxOnTime = (int)json["rfid_receiver"]["tx_on_time"];
                        rfid.nTxOffTime = (int)json["rfid_receiver"]["tx_off_time"];
                        rfid.nToggle = (int)json["rfid_receiver"]["toggle"];
                        rfid.nSpeakerlevel = (int)json["rfid_receiver"]["speaker_level"];
                        rfid.SPP_MAC = json["rfid_receiver"]["SPP_MAC"].ToString();
                        rfid.nRssi_pickup_timeout = (int)json["rfid_receiver"]["rssi_pickup_timeout"];
                        rfid.nRssi_pickup_threshold = (int)json["rfid_receiver"]["rssi_pickup_threshold"];
                        rfid.nRssi_drop_timeout = (int)json["rfid_receiver"]["rssi_drop_timeout"];
                        rfid.nRssi_drop_threshold = (int)json["rfid_receiver"]["rssi_drop_threshold"];
                        rfid.front_ant_port = json["rfid_receiver"]["front_ant_port"].ToString();

                        

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
