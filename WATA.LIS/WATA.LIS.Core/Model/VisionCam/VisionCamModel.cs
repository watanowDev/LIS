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
        public string QR { get; set; }
        public byte[] FRAME { get; set; }
        public bool connected = false;
    }
}
