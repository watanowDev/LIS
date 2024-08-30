using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;


namespace WATA.LIS.Core.Model.BackEnd
{
    public class TargetGeofence
    {
        public string mapId { get; set; }
        public string zoneId { get; set; }
        public string zoneName { get; set; }
        public string geom { get; set; }
    }

    public class Data
    {
        public string groupId { get; set; }
        public string groupName { get; set; }
        public List<TargetGeofence> targetGeofence { get; set; }
        public List<string> targetGeofenceIds { get; set; }
    }

    public class CellInfoModel
    {
        public int status { get; set; }
        public string message { get; set; }
        public object cause { get; set; }
        public object error { get; set; }
        public List<Data> data { get; set; }
        public long timeStamp { get; set; }
    }
}
