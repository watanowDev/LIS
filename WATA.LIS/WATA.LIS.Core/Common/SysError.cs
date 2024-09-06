using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Common
{
    public static class SysError
    {

        public static string CurrentError = "0000"; // Default : 0000, No Error


        public static string RFIDConnError = "0101";


        public static string DistanceConnError = "0201";


        public static string VisionConnError = "0301";


        public static string WeightConnError = "0401";
        public static string WeightLowBattery = "0402";


        public static string DisplayConnError = "0501";


        public static string DPSConnError = "0601";
        public static string DPSRcvError = "0602";


        public static string NAVConnError = "0701";



        public static void AddErrorCodes(params string[] codes)
        {
            foreach (var code in codes)
            {
                if (CurrentError.Contains(code))
                {
                    return;
                }

                if (CurrentError.Contains("0000"))
                {
                    var errorCodes = CurrentError.Split(',').ToList();
                    errorCodes.Remove("0000");
                    CurrentError = errorCodes.Count > 0 ? string.Join(",", errorCodes) : code;
                }
                else
                {
                    CurrentError += "," + code;
                }
            }
        }

        public static void RemoveErrorCodes(params string[] codes)
        {
            foreach (var code in codes)
            {
                if (CurrentError.Contains(code))
                {
                    var codeList = CurrentError.Split(',').ToList();
                    codeList.Remove(code);
                    CurrentError = codeList.Count > 0 ? string.Join(",", codeList) : "0000";
                }
            }
        }
    }
}
