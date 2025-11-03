using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class ActionInfoSDSAModel
    {
        public actionInfoSDSA actionInfo = new actionInfoSDSA();
    }

    public class actionInfoSDSA
    {
        public string wcId { get; set; }
        public string pidx { get; set; }
        public string vehicleId { get; set; }
        public string epc { get; set; }
        public int qty { get; set; }
        public string action { get; set; }
        public float visionWidth { get; set; }
        public float visionDepth { get; set; }
        public float visionHeight { get; set; }
        public string height { get; set; }
        public long x { get; set; } //x좌표
        public long y { get; set; } //y좌표
        public int t { get; set; } //지게차 헤딩 방향 (range : 0~3600)
        public List<LogisItem> logis { get; set; } = new List<LogisItem>(); // 팔레트/물류 적재 정보
        public class LogisItem
        {
            public string no { get; set; } // 아래 예시 참고
            public int floor { get; set; } // 팔레트 단(층) 수
            public string mn { get; set; } // Material Number (Product Code)
            public string sn { get; set; } //Serial ID (Number)

            /// <summary>
            /// no는 팔레트 적재 순서 
            /// 아래 삼각형 모양 2단 예시: "1.0", "2.0", "1.5"
            /// 0.0은 Back-End에서는 물류가 '없음'을 의미
            /// floor = 2               1.5 
            /// floor = 1            1.0 , 2.0
            /// </summary> 
        }
    }
}
