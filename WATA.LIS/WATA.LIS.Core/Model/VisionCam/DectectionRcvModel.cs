using System.Collections.Generic;
using Newtonsoft.Json;

namespace WATA.LIS.Core.Model.VisionCam
{
    /// <summary>
    /// ZeroMQ로 송신되는 detection 결과 전체를 담는 데이터 모델
    /// </summary>
    public class DectectionRcvModel
    {
        [JsonProperty("detections")]
        public List<DetectionItem> Detections { get; set; }

        [JsonProperty("pick_state")]
        public bool PickState { get; set; }

        [JsonProperty("nation_state")]
        public string NationState { get; set; }

        [JsonProperty("qrcode")]
        public List<string> QrCode { get; set; }
    }

    /// <summary>
    /// 단일 detection 객체 정보
    /// </summary>
    public class DetectionItem
    {
        [JsonProperty("box")]
        public List<int> Box { get; set; } // [x1, y1, x2, y2]

        [JsonProperty("confidence")]
        public float Confidence { get; set; }

        [JsonProperty("class")]
        public string Class { get; set; }
    }
}