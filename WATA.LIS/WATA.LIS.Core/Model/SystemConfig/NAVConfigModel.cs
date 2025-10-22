using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class NAVConfigModel : INAVModel
    {
        public int NAV_Enable { get; set; }
        public string Type { get; set; }
        public string IP { get; set; }
        public int PORT { get; set; }
        public int AdjustingPickdrop { get; set; }
        public int AdjustingPosition { get; set; }

        // 타임아웃 설정 (기본값 제공)
        public int FreezeThreshold { get; set; } = 20; // 100ms * 20 = 2초
        public int TransTimeoutCount { get; set; } = 10; // 127ms * 10 = 1.27초
        public int TransRetryMax { get; set; } = 5; // 재시도 최대 횟수
        public int ReconnectTimeoutSeconds { get; set; } = 2; // 재연결 타임아웃 (2초)
        public int SocketConnectTimeoutMs { get; set; } = 300; // 소켓 연결 타임아웃 (0.3초)
        public int MaxReceiveBufferSize { get; set; } = 4096; // 최대 수신 버퍼 크기
    }
}
