using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.SENSOR.NAV.NAV
{
    public class Globals
    {
        public const int count_size = 5;
        public const int nav_rcv = 0;

        //public static string nav_ip = "192.168.10.168";
        public static string nav_ip = "169.254.4.63";
        public static string nav_port = "2111";

        public static long nav_x;
        public static long nav_y;
        public static Int32 nav_dev;
        public static Int32 nav_phi;

        public static int pre_system_error = 0;

        public static int system_error = 0;
        public static int network_error = 0;

        public static int[] timer_count = new int[count_size];

        public static int nav_error_count = 0;

        public static int MQTT_commCheck = 0;
        public const string RESULT_FAIL = "F";
        public const string LOG_SettingDirPath = "C:\\WATA\\logs\\";

        public static byte[] strToHexByte(string hexString)
        {
            hexString = hexString.Replace(" ", "");
            if ((hexString.Length % 2) != 0)
                hexString += " ";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2).Trim(), 16);
            return returnBytes;
        }

        // Trans Byte To ASCII String
        public static string byteToString(byte data)
        {
            string hex = string.Concat(Array.ConvertAll(new byte[] { data }, byt => byt.ToString("X")));
            string temp = hex;
            StringBuilder sb = new StringBuilder(temp.Length * 2);
            foreach (byte b in temp)
            {
                sb.AppendFormat("{0:x2}", b);
            }
            return sb.ToString();
        }

        public static string stringToHexString(string hexString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in hexString)
                sb.AppendFormat("{0:X2}", (int)c);

            return sb.ToString().Trim();
        }

        public static void setTimerCounter(int id)
        {
            switch (id)
            {
                case nav_rcv:
                    timer_count[nav_rcv] = 1000; // 50 = 5000ms
                    break;
            }
        }

        public static void clearTimerCounter(int id)
        {
            switch (id)
            {
                case nav_rcv:
                    timer_count[nav_rcv] = 0;
                    break;
            }
        }

        public static void decTimerCount(Object state)
        {
            for (int i = 0; i < count_size; i++)
            {
                if (timer_count[i] > 0)
                    timer_count[i]--;
                else
                    timer_count[i] = 0;
            }

        }

        public static int getTimerCounter(int id)
        {
            return timer_count[id];
        }

    }
}
