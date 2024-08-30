using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.VISION
{
    public class VISON_Model
    {
        public float area { get; set; }
        public float width { get; set; }
        public int height { get; set; }
        public float depth { get; set; }


        public string qr { get; set; }
        public string status { get; set; }

        public byte[] matrix { get; set; }

        public bool has_roof { get; set; }

        public string simulation_status { get; set; }

        public string points { get; set; }  // LiDAR Point
    }
}


