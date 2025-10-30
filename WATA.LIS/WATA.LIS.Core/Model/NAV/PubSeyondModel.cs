using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.NAV
{
    public class PubSeyondModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Role { get; set; }
        public double Pitch { get; set; }
        public double Yaw { get; set; }
        public long PosX { get; set; }
        public long PosY { get; set; }
        public int PosH { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}