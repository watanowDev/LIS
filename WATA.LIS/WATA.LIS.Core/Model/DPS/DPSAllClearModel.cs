using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Model.DPS
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DPSAllClearModel
    {
        public byte SF { get; set; }
        public byte LEN { get; set; }
        public Payload payload { get; set; }
        public byte EF { get; set; }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Payload
        {
            public byte AckType { get; set; }
            public UInt16 LocationID { get; set; }
            public UInt16 ControllerID { get; set; }
        }

        public DPSAllClearModel()
        {
            SF = 0x35;
            EF = 0x3D;
            payload = new Payload();
            LEN = (byte)Marshal.SizeOf(payload);
        }
    }   
}


