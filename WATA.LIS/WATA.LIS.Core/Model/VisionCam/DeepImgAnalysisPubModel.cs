using Newtonsoft.Json;
using System.Collections.Generic;
using WATA.LIS.Core.Model.VisionCam;

namespace WATA.LIS.Core.Model.VisionCam
{
    public class DeepImgAnalysisPubModel
    {
        // ⭐ ImageBytes는 JSON 직렬화 시 Base64로 변환됨
        [JsonIgnore]
        public byte[] ImageBytes { get; set; }

        // ⭐ JSON에 포함될 Base64 이미지 필드
        [JsonProperty("Image")]
        public string ImageBase64
        {
            get {
                if (ImageBytes == null || ImageBytes.Length == 0)
                    return string.Empty;
                return System.Convert.ToBase64String(ImageBytes);
            }
        }

        [JsonProperty("ProductID")]
        public string ProductID { get; set; }

        [JsonProperty("zoneID")]
        public string ZoneID { get; set; }

        [JsonProperty("OcrList")]
        public List<string> OcrList { get; set; } = new List<string>();

        [JsonProperty("QR")]
        public List<string> QR { get; set; } = new List<string>();

        [JsonProperty("detections")]
        public List<DetectionItem> Detections { get; set; } = new List<DetectionItem>();
    }

    /// <summary>
    /// Python 테스트 코드 호환 포맷:
    /// {
    ///   "Image": "base64_encoded_string",
    ///   "ProductID": "6자리 난수",
    ///   "zoneID": "446dc087cc57873da3cc2198077ca034",
    ///   "OcrList": ["8430-01-536-5415"],
    ///   "QRList": ["6a7d3cf20a4a4f4b994230fe380c7d6f"],
    ///   "detections": [...]
    /// }
    /// 전송 형식: "LIS>RefineModel {JSON}"
    /// </summary>
}
