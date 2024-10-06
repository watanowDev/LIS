using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.LIVOX
{
    public class LIVOXModel
    {
        public string topic { get; set; }
        public int responseCode { get; set; } //물류 들었을 때 리복스로 pub 1 발행, 부피값 받았을 때 리스폰스 pub 0 발행
        public int height { get; set; }
        public int width { get; set; }
        public int depth { get; set; }
        public int result { get; set; } //리복스의 측정 결과값이 신뢰할 수 있는 값인지 아닌지 판단
    }
}
