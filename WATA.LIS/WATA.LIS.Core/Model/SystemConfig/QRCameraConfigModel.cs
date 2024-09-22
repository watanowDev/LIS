using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class QRCameraConfigModel : IQRCameraModel
    {
        public int qr_enable { get; set; }
        public string qr_cameraname { get; set; }
    }
}
