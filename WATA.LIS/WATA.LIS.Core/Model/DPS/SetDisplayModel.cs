using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Text.Unicode;
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
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF4;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF5;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF6;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF7;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF8;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF9;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF10;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF11;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF12;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF13;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF14;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF15;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] UTF16;
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


