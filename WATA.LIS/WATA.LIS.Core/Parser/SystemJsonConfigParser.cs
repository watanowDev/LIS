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
using System.Windows;

namespace WATA.LIS.Core.Parser
{
    public class SystemJsonConfigParser
    {
        public (WeightConfigModel, DistanceConfigModel, VisionConfigModel, 
                RFIDConfigModel, MainConfigModel, Led_Buzzer_ConfigModel, 
                DPSConfigModel, NAVConfigModel, VisionCamConfigModel, 
                LIVOXConfigModel, DisplayConfigModel) LoadJsonfile()
        {
            WeightConfigModel weight = new WeightConfigModel();
            DistanceConfigModel distance = new DistanceConfigModel();
            VisionConfigModel vision = new VisionConfigModel();
            MainConfigModel main = new MainConfigModel();
            RFIDConfigModel rfid = new RFIDConfigModel();
            Led_Buzzer_ConfigModel LedBuzzer = new Led_Buzzer_ConfigModel();
            DPSConfigModel dps = new DPSConfigModel();
            NAVConfigModel nav = new NAVConfigModel();
            VisionCamConfigModel visioncam = new VisionCamConfigModel();
            LIVOXConfigModel livox = new LIVOXConfigModel();
            DisplayConfigModel display = new DisplayConfigModel();

            try
            {
                string path = System.IO.Directory.GetCurrentDirectory() + "\\SystemConfig\\SystemConfig.json";
                using (StreamReader file = File.OpenText(path))
                {
                    using (JsonTextReader reader = new JsonTextReader(file))
                    {
                        JObject json = (JObject)JToken.ReadFrom(reader);


                        main.device_type = json["main"]["device_type"].ToString();
                        main.projectId = json["main"]["projectId"].ToString();
                        main.mappingId = json["main"]["mappingId"].ToString();
                        main.mapId = json["main"]["mapId"].ToString();
                        main.vehicleId = json["main"]["vehicleId"].ToString();


                        distance.distance_enable = (int)json["distance_sensor"]["distance_enable"];
                        distance.model_name = json["distance_sensor"]["model_name"].ToString();
                        distance.ComPort = json["distance_sensor"]["comport"].ToString();
                        distance.pick_up_distance_threshold = (int)json["distance_sensor"]["pick_up_distance_threshold"];


                        LedBuzzer.led_enable = (int)json["led_buzzer"]["led_enable"];
                        LedBuzzer.volume = (int)json["led_buzzer"]["volume"];
                        LedBuzzer.InfoLanguage = json["led_buzzer"]["InfoLanguage"].ToString();


                        weight.weight_enable = (int)json["weight_sensor"]["weight_enable"];
                        weight.ComPort = json["weight_sensor"]["comport"].ToString();
                        weight.loadweight_timeout = (int)json["weight_sensor"]["loadweight_timeout"];
                        weight.sensor_value = json["weight_sensor"]["sensor_value"].ToString();


                        vision.vision_enable = (int)json["visioncamera"]["vision_enable"];
                        vision.CameraHeight = (float)json["visioncamera"]["camera_height"];
                        vision.QRValue = (int)json["visioncamera"]["qr_enable"];
                        vision.view_3d_enable = (int)json["visioncamera"]["view_3d_enable"];
                        vision.event_distance = (float)json["visioncamera"]["event_distance"];
                        vision.pickup_wait_delay = (int)json["visioncamera"]["pickup_wait_delay"];
                        vision.rack_with = (float)json["visioncamera"]["rack_with"];
                        vision.rack_height = (float)json["visioncamera"]["rack_height"];
                        vision.onlyshelf = (int)json["visioncamera"]["onlyshelf"];


                        rfid.rfid_enable = (int)json["rfid_receiver"]["rfid_enable"];
                        rfid.rfid_name = json["rfid_receiver"]["rfid_name"].ToString();
                        rfid.comport = json["rfid_receiver"]["comport"].ToString();
                        rfid.SPP_MAC = json["rfid_receiver"]["SPP_MAC"].ToString();
                        rfid.nRadioPower = (int)json["rfid_receiver"]["radio_power"];
                        rfid.nTxOnTime = (int)json["rfid_receiver"]["tx_on_time"];
                        rfid.nTxOffTime = (int)json["rfid_receiver"]["tx_off_time"];
                        rfid.nToggle = (int)json["rfid_receiver"]["toggle"];
                        rfid.nSpeakerlevel = (int)json["rfid_receiver"]["speaker_level"];
                        rfid.nRssi_pickup_timeout = (int)json["rfid_receiver"]["rssi_pickup_timeout"];
                        rfid.nRssi_pickup_threshold = (int)json["rfid_receiver"]["rssi_pickup_threshold"];
                        rfid.nRssi_drop_timeout = (int)json["rfid_receiver"]["rssi_drop_timeout"];
                        rfid.nRssi_drop_threshold = (int)json["rfid_receiver"]["rssi_drop_threshold"];
                        rfid.front_ant_port = json["rfid_receiver"]["front_ant_port"].ToString();
                        rfid.ip = json["rfid_receiver"]["ip"].ToString();


                        dps.IP = json["DPS"]["IP"].ToString();
                        dps.PORT = (int)json["DPS"]["PORT"];


                        nav.NAV_Enable = (int)json["NAV"]["NAV_Enable"];
                        nav.IP = json["NAV"]["IP"].ToString();
                        nav.PORT = (int)json["NAV"]["PORT"];


                        visioncam.vision_enable = (int)json["vision"]["vision_enable"];
                        visioncam.vision_name = json["vision"]["vision_name"].ToString();
                        visioncam.vision_ip = json["vision"]["vision_ip"].ToString();
                        visioncam.vision_port = Convert.ToInt16(json["vision"]["vision_port"]);
                        visioncam.vision_id = json["vision"]["vision_id"].ToString();
                        visioncam.vision_pw = json["vision"]["vision_pw"].ToString();


                        display.display_enable = (int)json["display"]["display_enable"];

                        Tools.Log($"Load SystemConfig {json.ToString()}", Tools.ELogType.SystemLog);
                    }
                }
            }
            catch (Exception Ex)
            {
                Tools.Log($"Exception !!!", Tools.ELogType.DistanceLog);
                MessageBox.Show("Config File Failed");
                Console.WriteLine(Ex.Message);
            }

            return (weight, distance, vision, rfid, main, LedBuzzer, dps, nav, visioncam, livox, display);
        }
    }
}
