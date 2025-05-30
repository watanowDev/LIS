using System;

namespace WATA.LIS.Core.Model.ErrorCheck
{
    public class SensorReady
    {
        // 싱글톤 인스턴스
        private static readonly Lazy<SensorReady> _instance = new Lazy<SensorReady>(() => new SensorReady());

        // 센서 상태 속성
        public bool Weight { get; set; }
        public bool Distance { get; set; }
        public bool RFID { get; set; }
        public bool VisionCam { get; set; }
        public bool Lidar2D { get; set; }
        //public bool Lidar3D { get; set; }
        public bool Indicator { get; set; }

        // private 생성자: 외부에서 인스턴스 생성 불가
        private SensorReady()
        {
            // 초기 상태 설정
            Weight = false;
            Distance = false;
            RFID = false;
            VisionCam = false;
            Lidar2D = false;
            //Lidar3D = false;
            Indicator = false;
        }

        // 싱글톤 인스턴스 접근
        public static SensorReady Instance => _instance.Value;
    }
}