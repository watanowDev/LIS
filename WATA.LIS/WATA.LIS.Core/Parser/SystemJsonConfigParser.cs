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
        public (WeightConfigModel, DistanceConfigModel,
                RFIDConfigModel, MainConfigModel, Led_Buzzer_ConfigModel, 
                NAVConfigModel, VisionCamConfigModel, 
                LIVOXConfigModel, DisplayConfigModel) LoadJsonfile()
        {
            WeightConfigModel weight = new WeightConfigModel();
            DistanceConfigModel distance = new DistanceConfigModel();
            MainConfigModel main = new MainConfigModel();
            RFIDConfigModel rfid = new RFIDConfigModel();
            Led_Buzzer_ConfigModel LedBuzzer = new Led_Buzzer_ConfigModel();
            NAVConfigModel nav = new NAVConfigModel();
            VisionCamConfigModel visioncam = new VisionCamConfigModel();
            LIVOXConfigModel livox = new LIVOXConfigModel();
            DisplayConfigModel display = new DisplayConfigModel();

            try
            {
                // Single-file publish 환경에서도 작동하도록 경로 계산
                // 1. 실행 파일의 실제 위치 (단일 파일이 아닌 경우)
                string exeLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // 2. 단일 파일 publish의 경우 Location이 비어있을 수 있으므로 BaseDirectory 사용
                if (string.IsNullOrEmpty(exeLocation) || !File.Exists(exeLocation))
                {
                    exeLocation = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? baseDir;
                }
                
                // 3. 실행 파일이 있는 디렉터리 기준으로 Config 경로 설정
                string configDir = Path.GetDirectoryName(exeLocation);
                if (string.IsNullOrEmpty(configDir))
                {
                    configDir = baseDir;
                }
                
                string path = Path.Combine(configDir, "SystemConfig", "SystemConfig.json");
                
                Tools.Log($"Config path: {path}", Tools.ELogType.SystemLog);
                Tools.Log($"Config exists: {File.Exists(path)}", Tools.ELogType.SystemLog);
                
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"SystemConfig.json not found at: {path}");
                }
 
                using (StreamReader file = File.OpenText(path))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    // Allow comments and be lenient
                    var loadSettings = new JsonLoadSettings
                    {
                        CommentHandling = CommentHandling.Ignore,
                        LineInfoHandling = LineInfoHandling.Load
                    };
                    JObject json = (JObject)JToken.ReadFrom(reader, loadSettings);

                    // helpers
                    static string S(JToken tok, string def = "")
                    {
                        if (tok == null || tok.Type == JTokenType.Null) return def;
                        return tok.Type == JTokenType.String ? (string)tok : tok.ToString();
                    }
                    static int I(JToken tok, int def = 0)
                    {
                        try
                        {
                            if (tok == null || tok.Type == JTokenType.Null) return def;
                            if (tok.Type == JTokenType.Integer) return (int)tok;
                            if (tok.Type == JTokenType.Float) return (int)Math.Round((double)tok);
                            if (int.TryParse(tok.ToString(), out var v)) return v;
                            return def;
                        }
                        catch { return def; }
                    }

                    var mainJ = json["main"] as JObject ?? new JObject();
                    main.device_type = S(mainJ["device_type"], main.device_type);
                    main.projectId = S(mainJ["projectId"], main.projectId);
                    main.mappingId = S(mainJ["mappingId"], main.mappingId);
                    main.mapId = S(mainJ["mapId"], main.mapId);
                    main.vehicleId = S(mainJ["vehicleId"], main.vehicleId);
                    // optional tuning: action dedup window in ms
                    try
                    {
                        var adedup = mainJ["action_dedup_ms"];
                        if (adedup != null && adedup.Type != JTokenType.Null)
                            main.action_dedup_ms = I(adedup, 300);
                    }
                    catch { }

                    // database optional settings (host/port/db/user/pw/search_path)
                    try
                    {
                        var dbJ = mainJ["db"] as JObject ?? new JObject();
                        main.db_host = S(dbJ["host"], main.db_host);
                        var portTok = dbJ["port"]; if (portTok != null && portTok.Type != JTokenType.Null) main.db_port = I(portTok, main.db_port ?? 5432);
                        main.db_database = S(dbJ["database"], main.db_database);
                        main.db_username = S(dbJ["username"], main.db_username);
                        main.db_password = S(dbJ["password"], main.db_password);
                        main.db_search_path = S(dbJ["search_path"], main.db_search_path);
                    }
                    catch { }

                    // distance
                    var distJ = (json["distance_sensor"] ?? json["distancesensor"] ?? json["distanceSensor"]) as JObject ?? new JObject();
                    distance.distance_enable = I(distJ["distance_enable"], distance.distance_enable);
                    distance.model_name = S(distJ["model_name"], distance.model_name);
                    distance.ComPort = S(distJ["comport"], distance.ComPort);
                    distance.pick_up_distance_threshold = I(distJ["pick_up_distance_threshold"], distance.pick_up_distance_threshold);

                    // led/buzzer
                    var ledJ = (json["led_buzzer"] ?? json["ledBuzzer"] ?? json["led"]) as JObject ?? new JObject();
                    LedBuzzer.led_enable = I(ledJ["led_enable"], LedBuzzer.led_enable);
                    LedBuzzer.OnlySpeark = I(ledJ["OnlySpeark"], LedBuzzer.OnlySpeark);
                    LedBuzzer.volume = I(ledJ["volume"], LedBuzzer.volume);
                    LedBuzzer.lamp_IP = S(ledJ["lamp_IP"], LedBuzzer.lamp_IP);
                    LedBuzzer.InfoLanguage = S(ledJ["InfoLanguage"], LedBuzzer.InfoLanguage);

                    // weight
                    var weightJ = (json["weight_sensor"] ?? json["weightsensor"] ?? json["weightSensor"]) as JObject ?? new JObject();
                    weight.weight_enable = I(weightJ["weight_enable"], weight.weight_enable);
                    weight.ComPort = S(weightJ["comport"], weight.ComPort);
                    weight.loadweight_timeout = I(weightJ["loadweight_timeout"], weight.loadweight_timeout);
                    weight.sensor_value = S(weightJ["sensor_value"], weight.sensor_value);

                    // rfid
                    var rfidJ = json["rfid_receiver"] as JObject ?? new JObject();
                    rfid.rfid_enable = I(rfidJ["rfid_enable"], rfid.rfid_enable);
                    rfid.rfid_name = S(rfidJ["rfid_name"], rfid.rfid_name);
                    rfid.comport = S(rfidJ["comport"], rfid.comport);
                    rfid.SPP_MAC = S(rfidJ["SPP_MAC"], rfid.SPP_MAC);
                    rfid.nRadioPower = I(rfidJ["radio_power"], rfid.nRadioPower);
                    rfid.nTxOnTime = I(rfidJ["tx_on_time"], rfid.nTxOnTime);
                    rfid.nTxOffTime = I(rfidJ["tx_off_time"], rfid.nTxOffTime);
                    rfid.nToggle = I(rfidJ["toggle"], rfid.nToggle);
                    rfid.nSpeakerlevel = I(rfidJ["speaker_level"], rfid.nSpeakerlevel);
                    rfid.nRssi_pickup_timeout = I(rfidJ["rssi_pickup_timeout"], rfid.nRssi_pickup_timeout);
                    rfid.nRssi_pickup_threshold = I(rfidJ["rssi_pickup_threshold"], rfid.nRssi_pickup_threshold);
                    rfid.nRssi_drop_timeout = I(rfidJ["rssi_drop_timeout"], rfid.nRssi_drop_timeout);
                    rfid.nRssi_drop_threshold = I(rfidJ["rssi_drop_threshold"], rfid.nRssi_drop_threshold);
                    rfid.front_ant_port = S(rfidJ["front_ant_port"], rfid.front_ant_port);
                    rfid.ip = S(rfidJ["ip"], rfid.ip);

                    // NAV
                    var navJ = json["NAV"] as JObject ?? new JObject();
                    nav.NAV_Enable = I(navJ["NAV_Enable"], nav.NAV_Enable);
                    nav.Type = S(navJ["Type"], nav.Type);
                    nav.IP = S(navJ["IP"], nav.IP);
                    nav.PORT = I(navJ["PORT"], nav.PORT);
                    nav.AdjustingPickdrop = I(navJ["AdjustingPickdrop"], nav.AdjustingPickdrop);
                    nav.AdjustingPosition = I(navJ["AdjustingPosition"], nav.AdjustingPosition);

                    // LIVOX
                    var livoxJ = json["LIVOX"] as JObject ?? new JObject();
                    livox.LIVOX_Enable = I(livoxJ["LIVOX_Enable"], livox.LIVOX_Enable);

                    // vision (support both 'vision', 'visioncamera', 'QR')
                    var visionJ = (json["vision"] ?? json["visioncamera"] ?? json["QR"]) as JObject ?? new JObject();
                    visioncam.vision_enable = I(visionJ["vision_enable"], visioncam.vision_enable);
                    visioncam.vision_name = S(visionJ["vision_name"], visioncam.vision_name);
                    visioncam.vision_ip = S(visionJ["vision_ip"], visioncam.vision_ip);
                    visioncam.vision_port = (short)I(visionJ["vision_port"], visioncam.vision_port);
                    visioncam.vision_id = S(visionJ["vision_id"], visioncam.vision_id);
                    visioncam.vision_pw = S(visionJ["vision_pw"], visioncam.vision_pw);

                    // display (optional)
                    var displayJ = json["display"] as JObject ?? new JObject();
                    display.display_enable = I(displayJ["display_enable"], display.display_enable);
                    display.display_type = S(displayJ["display_type"], display.display_type);

                    // ✅ 디버깅: display 설정 파싱 결과 로그
                    Tools.Log($"[CONFIG PARSER] display_enable parsed = {display.display_enable}", Tools.ELogType.SystemLog);
                    Tools.Log($"[CONFIG PARSER] display_type parsed = {display.display_type ?? "null"}", Tools.ELogType.SystemLog);
                    Tools.Log($"[CONFIG PARSER] display JSON section exists = {json["display"] != null}", Tools.ELogType.SystemLog);

                    Tools.Log($"Load SystemConfig OK", Tools.ELogType.SystemLog);
                }
            }
            catch (Exception Ex)
            {
                Tools.Log($"Config parse error: {Ex.Message}", Tools.ELogType.SystemLog);
                MessageBox.Show("Config File Failed");
            }

            return (weight, distance, rfid, main, LedBuzzer, nav, visioncam, livox, display);
        }
    }
}
