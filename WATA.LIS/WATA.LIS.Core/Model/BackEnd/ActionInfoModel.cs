using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class ActionInfoModel
    {
        public actionInfo actionInfo = new actionInfo();
    }

    public class actionInfo
    {
        public string workLocationId { get; set; }
        public string projectId { get; set; }
        public string mappingId { get; set; }
        public string mapId { get; set; }
        public string vehicleId { get; set; }
        public string action { get; set; }
        public string loadRate { get; set; }
        public float loadWeight { get; set; }
        public string epc { get; set; }
        public string cepc { get; set; }
        public string loadId { get; set; }
        public bool shelf { get; set; }
        public string height { get; set; }
        public float visionWidth { get; set; }
        public float visionHeight { get; set; }
        public float visionDepth { get; set; }
        public string loadMatrixRaw { get; set; }
        public string loadMatrixColumn { get; set; }
        public List<byte> loadMatrix { get; set; } = new List<byte>();


        public string zoneId { get; set; }      // 평치측위 NavSensor
        public string zoneName { get; set; }    // 평치측위 NavSensor

        public string plMatrix { get; set; }    // 형상측위 LiDAR Point
    }
}
