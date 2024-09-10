using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.BackEnd
{
    public class ProdDataModel
    {
        public int pidx { get; set; } //프로젝트 id
        public int vidx { get; set; } //지게차 id
        public string vehicleId { get; set; } //지게차 이름
        public long x { get; set; } //x좌표
        public long y { get; set; } //y좌표
        public int t { get; set; } //지게차 헤딩 방향 (range : 0~3600)
        public int move { get; set; } //0:stop, 1:move
        public int load { get; set; } //0:unload, 1:load
        public int result { get; set; } //평치측위 상태리턴 (1:정상, 1 이외 값:비정상)
        public string errorCode { get; set; } //지게차 하위 edge-device의 ConnErr, Warning, Alarm
    }
}
