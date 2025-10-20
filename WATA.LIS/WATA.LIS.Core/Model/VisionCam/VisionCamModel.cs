using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ZXing;
using System.Drawing;

namespace WATA.LIS.Core.Model.VisionCam
{
    public class VisionCamModel
    {
        public string FRAME_ID { get; set; } = string.Empty;
        public string QR { get; set; } = string.Empty;
        public List<V2DetectionModel> Objects { get; set; } = new List<V2DetectionModel>();
        public int WIDTH { get; set; }
        public int HEIGHT { get; set; }
        public float ACTION_DEPTH { get; set; }
        public double BR_DEPTH { get; set; }
        public double BL_DEPTH { get; set; }
        public double MR_DEPTH { get; set; }
        public double ML_DEPTH { get; set; }
        public double TR_DEPTH { get; set; }
        public double TL_DEPTH { get; set; }
        public double FPS { get; set; }

        public string POINTS = string.Empty; // 형상측정값
        public byte[] FRAME { get; set; }
        public bool connected = false;
    }
}
