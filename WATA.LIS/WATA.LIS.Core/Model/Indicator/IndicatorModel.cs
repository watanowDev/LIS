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
    }

    public class forklift_status
    {
        public int command { get; set; } // 0. alive, 1. loaded, 2. 상차요청응답, 3. 하차요청응답, 4. 상차중드롭이벤트, 5. 하차중드롭이벤트

        public string QR { get; set; }
        public int weightTotal { get; set; }
        public float visionHeight { get; set; }
        public float visionWidth { get; set; }
        public float visionDepth { get; set; }
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
        /// command = 0일 때 : 디폴트 상태. 1초마다 발송.
        /// command = 1일 때 : 물류를 들고있는 상태. 1초마다 발송. 이 상태에서 command = 2가 들어왔을 경우 상차할 컨테이너를 EPC 데이터 근거로 정상적인 컨테이너에 진입하는지를 인디케이터에서 판단한다.
        /// command = 2일 때 : 인디케이터에서 상차 요청을 받은 상태. 요청을 받았을 때 한번만 발송함. 해당 상태를 기억하고 있다가 물류 드롭 완료 시 command = 4로 변경함.
        /// command = 3일 때 : 인디케이터에서 하차 요청을 받은 상태. 요청을 받았을 때 한번만 발송함. 해당 상태를 기억하고 있다가 물류 드롭 완료 시 command = 5로 변경함.
        /// command = 4일 때 : 상차 요청을 받은 상태에서 물류 드롭 완료 했을 때 한번만 발송함.
        /// command = 5일 때 : 하차 요청을 받은 상태에서 물류 드롭 완료 했을 때 한번만 발송함.
    }
}
