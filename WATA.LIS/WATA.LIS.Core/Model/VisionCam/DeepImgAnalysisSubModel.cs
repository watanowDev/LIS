using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.VisionCam
{
    public class DeepImgAnalysisSubModel
    {
        // JSON 데이터 필드
        [JsonProperty("ProductID")]
        public string ProductID { get; set; }

        [JsonProperty("OcrList")]
        public List<string> OcrList { get; set; } = new List<string>();

        [JsonProperty("QRList")]
        public List<string> QR { get; set; } = new List<string>();

        [JsonProperty("detections")]
        public List<DetectionItem> Detections { get; set; } = new List<DetectionItem>();
    }
}
