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


        public static string NAVConnError = "0701";



        public static void AddErrorCode(string code)
        {
            if (CurrentError.Contains("0000"))
            {
                CurrentError = code;
            }
            else
            {
                CurrentError += "," + code;
            }
        }

        public static void RemoveErrorCode(string code)
        {
            if (CurrentError.Contains(code))
            {
                var codes = CurrentError.Split(',').ToList();
                codes.Remove(code);
                CurrentError = codes.Count > 0 ? string.Join(",", codes) : "0000";
            }
        }
    }
}
