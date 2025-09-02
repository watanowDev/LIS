using Newtonsoft.Json;
using System.Collections.Generic;
using WATA.LIS.Core.Model.VisionCam;

namespace WATA.LIS.Core.Model.VisionCam
{
    public class DeepImgAnalysisPubModel
    {

        // ���� �̹��� ���̳ʸ��� �޽����� ù ��° ���������� ���� (JSON���� �������� ����)
        public byte[] ImageBytes { get; set; }

        // JSON ������ �ʵ�
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
    /// "Image" : ���� �̹��� (������ multipart ù ����������)
    /// "ProductID": str # ��� Ȯ���� ���� Ű ��(6�ڸ� ����)
    /// "zoneID" : str # �� id : ex)446dc087cc57873da3cc2198077ca034
    /// "OcrList" : list # �𵨿��� ocr_result �� ���� �ִ°� 
    /// "QRList": str # QR�������� ��, �ƴϸ� QR����
    /// "detections" : list # �𵨿��� detections �� ���� �ִ°�
    /// 5003�� ����, 5004�� ����
    /// </summary>
}
