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
    public class SetDisplayModel    {
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
            public byte ADDR1 { get; set; }
            public byte ADDR2 { get; set; }
            public byte SEQ { get; set; }
            public byte COLORSET1 { get; set; }
            public byte COLORSET2 { get; set; }

            public byte COLORSET3 { get; set; }

            public byte COLORSET4 { get; set; }

            public byte COLORSET5 { get; set; }
            public byte COLORSET6 { get; set; }
            public byte COLORSET7 { get; set; }
            public byte COLORSET8 { get; set; }
            public byte COLORSET9 { get; set; }
            public byte COLORSET10 { get; set; }
            public byte COLORSET11 { get; set; }
            public byte COLORSET12 { get; set; }
            public byte COLORSET13 { get; set; }
            public byte COLORSET14 { get; set; }
            public byte COLORSET15 { get; set; }
            public byte COLORSET16 { get; set; }
            public string code1 { get; set; }
            public string code2 { get; set; }
            public string code3 { get; set; }
            public string code4 { get; set; }
            public string code5 { get; set; }
            public string code6 { get; set; }
            public string code7 { get; set; }
            public string code8 { get; set; }
            public string code9 { get; set; }
            public string code10 { get; set; }
            public string code11 { get; set; }
            public string code12 { get; set; }
            public string code13 { get; set; }
            public string code14 { get; set; }
            public string code15 { get; set; }
            public string code16 { get; set; }
        }

        public SetDisplayModel()
        {
            SF = 0x25;
            EF = 0x2D;
            payload = new Payload();
            LEN = (byte)Marshal.SizeOf(payload);
        }
    }   
}


