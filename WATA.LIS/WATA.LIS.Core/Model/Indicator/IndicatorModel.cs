using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Core.Model.Indicator
{
    public class IndicatorModel
    {
        public forklift_status forklift_status = new forklift_status();
        public bool tail = true;
    }

    public class forklift_status
    {
        public int command { get; set; } // -1. 물류 측정중(QR인식된 상태), 0. normal, 1. normal_load, 2. 상차중load, 3. 하차중load

        public string QR { get; set; }
        public int weightTotal { get; set; }
        public float visionHeight { get; set; }
        public float visionWidth { get; set; }
        public float visionDepth { get; set; }
        public string points { get; set; } // pcd, point cloud data
        public string epc { get; set; }

        public bool networkStatus { get; set; } // 외부망 연결 상태
        public bool weightSensorStatus { get; set; }
        public bool visionCamStatus { get; set; }
        public bool lidar2dStatus { get; set; }
        public bool lidar3dStatus { get; set; }
        public bool heightSensorStatus { get; set; }
        public bool rfidStatus { get; set; }



        /// <summary>
        /// 인디케이터 상태 관련 command 설명
        /// </summary>
        /// 통신주기 = 1s
        /// command = 1 전환 : command = 0, 2, 3인 상태에서 물류를 픽업함. response : set_load
        /// command = 2 전환 : command = 0, 1, 3인 상태에서 인디케이터의 상차 요청을 받음. response : set_unload
        /// command = 3 전환 : command = 0, 1, 2인 상태에서 인디케이터의 하차 요청을 받음. response : set_unload
        /// command = 0 전환 : command = 1, 2, 3인 상태에서 물류 드롭 완료. 
    }
}
