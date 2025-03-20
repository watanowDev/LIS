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
    public class VisionDetectionModel
    {
        public string Timestamp { get; set; }
        public Detection[] Detections { get; set; }
    }

    public class Detection
    {
        public string Label { get; set; }
        public float Confidence { get; set; }
        public float Xmin { get; set; }
        public float Ymin { get; set; }
        public float Xmax { get; set; }
        public float Ymax { get; set; }
        public float Distance { get; set; }
    }
}
