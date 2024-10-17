using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Common
{
    public static class SysAlarm
    {

        public static string CurrentErr = "0000"; // Default : 0000, No Error
        public static string NetErr = "0001";


        public static string RFIDConnErr = "0101";
        public static string RFIDRcvErr = "0102";
        public static string RFIDStartErr = "0103";


        public static string DistanceConnErr = "0201";


        public static string VisionConnErr = "0301"; // QR Camera
        public static string VisionRcvErr = "0302";


        public static string WeightConnErr = "0401";
        public static string WeightLowBattery = "0402";


        public static string IndicatorConnErr = "0501"; // App Display


        public static string DPSConnErr = "0601";
        public static string DPSRcvErr = "0602";


        public static string NAVConnErr = "0701";



        public static void AddErrorCodes(params string[] codes)
        {
            foreach (var code in codes)
            {
                if (CurrentErr.Contains(code))
                {
                    break;
                }

                if (CurrentErr.Contains("0000"))
                {
                    var errorCodes = CurrentErr.Split(',').ToList();
                    errorCodes.Remove("0000");
                    CurrentErr = errorCodes.Count > 0 ? string.Join(",", errorCodes) : code;
                }
                else
                {
                    CurrentErr += "," + code;
                }
            }
        }

        public static void RemoveErrorCodes(params string[] codes)
        {
            foreach (var code in codes)
            {
                if (CurrentErr.Contains(code))
                {
                    var codeList = CurrentErr.Split(',').ToList();
                    codeList.RemoveAll(c => c == code);
                    CurrentErr = codeList.Count > 0 ? string.Join(",", codeList) : "0000";
                }
            }
        }
    }
}
