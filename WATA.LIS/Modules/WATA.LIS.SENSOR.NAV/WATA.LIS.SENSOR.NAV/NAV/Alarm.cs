using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.SENSOR.NAV.NAV
{
    public class Alarms
    {
        public static int ALARM_NONE = 0;
        public static int ALARM_NAV350_SOCKET_OPEN_ERROR = 1;
        public static int ALARM_NAV350_LOGIN = 2;
        public static int ALARM_NAV350_MODE_CHANGE_INVALID_CHANGE = 3;
        public static int ALARM_NAV350_MODE_CHANGE_METHOD_BREAK = 4;
        public static int ALARM_NAV350_MODE_CHANGE_UNKNOWN_OP_MODE = 5;
        public static int ALARM_NAV350_MODE_CHANGE_TIMEOUT = 6;
        public static int ALARM_NAV350_MODE_CHANGE_ANOTHER_CMD_ACTIVE = 7;
        public static int ALARM_NAV350_MODE_CHANGE_GENERAL_ERROR = 8;
        public static int ALARM_NAV350_POSE_WRONG_OP_MODE = 9;
        public static int ALARM_NAV350_POSE_ASYNC_TERMINATED = 10;
        public static int ALARM_NAV350_POSE_INVALID_DATA = 11;
        public static int ALARM_NAV350_POSE_NO_POS_AVAILABLE = 12;
        public static int ALARM_NAV350_POSE_TIMEOUT = 13;
        public static int ALARM_NAV350_POSE_METHOD_ALREADY_ACTIVE = 14;
        public static int ALARM_NAV350_POSE_GENERAL_ERROR = 15;
        public static int ALARM_NAV350_POSE_UNKNOWN_ERROR = 16;
        public static int ALARM_NAV350_SET_LAYER = 17;
        public static int ALARM_NAV350_SET_DATA_FORMAT = 18;
        public static int ALARM_NAV350_COMM_ERROR = 19;
        public static int ALARM_NAV350_COMM_INDEX_ERROR = 20;
        public static int ALARM_NAV350_COMM_CMD_ERROR = 21;
        public static int ALARM_NAV350_TRANSMIT_ERROR = 22;
        public static int ALARM_NAV350_CONNECTION_ERROR = 23;
        public static int ALARM_NAV350_RESET = 25;

                
        public const int ALARM_SYSTEM_UNKNOWN_ERROR = 199;       
        public const int ALARM_PROTOCOL_FORMAT_ERROR = 210;
    }
}
