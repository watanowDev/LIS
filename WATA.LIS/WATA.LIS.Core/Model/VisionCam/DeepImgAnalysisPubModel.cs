using Newtonsoft.Json;
using System.Collections.Generic;
using WATA.LIS.Core.Model.VisionCam;

namespace WATA.LIS.Core.Model.VisionCam
{
    public class DeepImgAnalysisPubModel
    {

        // 원본 이미지 바이너리는 메시지의 첫 번째 프레임으로 전송 (JSON에는 포함하지 않음)
        public byte[] ImageBytes { get; set; }

        // JSON 데이터 필드
        [JsonProperty("ProductID")]
        public string ProductID { get; set; }

        [JsonProperty("zoneID")]
        public string ZoneID { get; set; }

        [JsonProperty("OcrList")]
        public List<string> OcrList { get; set; } = new List<string>();

        [JsonProperty("QRList")]
        public List<string> QR { get; set; } = new List<string>();

        [JsonProperty("detections")]
        public List<DetectionItem> Detections { get; set; } = new List<DetectionItem>();
    }

    /// <summary>
    /// "Image" : 원본 이미지 (전송은 multipart 첫 프레임으로)
    /// "ProductID": str # 결과 확인을 위한 키 값(6자리 난수)
    /// "zoneID" : str # 존 id : ex)446dc087cc57873da3cc2198077ca034
    /// "OcrList" : list # 모델에서 ocr_result 로 던저 주는거 
    /// "QRList": str # QR못읽으면 빈값, 아니면 QR정보
    /// "detections" : list # 모델에서 detections 로 던저 주는거
    /// 5003은 발행, 5004는 구독
    /// </summary>
}
