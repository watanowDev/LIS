using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;
using Prism.Events;
using System.Threading;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.NAV;
using System.Net.Sockets;
using System.Text;
using System;
using WATA.LIS.SENSOR.NAV.NAV;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Events.NAVSensor;

namespace WATA.LIS.SENSOR.NAV
{
    public class NAVSensor
    {
        Thread RecvThread;

        private readonly IEventAggregator _eventAggregator;
        public static Socket socketSend;

        const int ERROR_NAV_RESET_COUNT = 100;

        static byte gNAVCommand = 0;

        static byte gNAVTransTimeOutCount;
        static byte gNAVTransRetryCount;
        static bool gNAVEnable;

        static uint gResetCount = 0;

        static uint gNAVPosCount = 0;

        static uint gNAVResetCount;

        static string[] gNAVRcvBuff = new string[128];

        static byte gNAVstate;
        static byte gNAVLayerCmd = 0;

        static uint gPositionErrCnt;
        static string gNAVTranBuff;

        private static int gTranscheck = 2;
        static int gCopyBufferNavCmd = 0;

        private static bool gNavTransErrorCheck = false;
        private static bool gNavRcvErrorCheck = false;

        private static int positionErrCount = 0;
        private static int positionNavCount = 0;

        public bool nav350_socket_open = false;
        bool nav350_socket_open_once = false;
        Thread nav350TransThread;
        Thread nav350RcvThread;
        System.Threading.Timer countTimer;
        Thread mainProcess;

        private static string gIP = "169.254.4.63";
        private static string gPORT = "2111";

        public NAVSensor(IEventAggregator eventAggregator, INAVModel navModel)
        {
            _eventAggregator = eventAggregator;

            NAVConfigModel _navConfig = (NAVConfigModel)navModel;
            gIP = _navConfig.IP;
            gPORT = _navConfig.PORT.ToString(); ;
        }

        public void Init()
        {
            Tools.Log($"Init NAV", Tools.ELogType.NAVLog);

            mainProcess = new Thread(Main);
            mainProcess.Start();

        }

        public void Main()
        {
            // [NAV FREEZE CHECK] 이전 NAV 값과 변화 없는 횟수 저장 변수 추가
            long prevNavX = Globals.nav_x;
            long prevNavY = Globals.nav_y;
            long prevNavT = Globals.nav_phi;
            int navFreezeCount = 0;
            const int navFreezeThreshold = 30; // 100ms * 30 = 3초

            while (true)
            {
                if (nav350_socket_open == true)
                {
                    if (nav350_socket_open_once == false)
                    {
                        nav350TransThread.Start();
                        nav350RcvThread.Start();

                        nav350_socket_open_once = true;
                    }

                    if (Globals.getTimerCounter(Globals.nav_rcv) == 0) // NAV350 통신 연결 상태 확인
                    {
                        if (nav350_socket_open_once == true)
                        {
                            //nav350TransThread.Abort();
                            //nav350RcvThread.Abort();
                            nav350_socket_open_once = false;
                        }

                        NAVSensor.socketSend.Close();
                        NAVSensor.socketSend.Dispose();
                        NAVSensor.NAV_SoftWareReset();

                        Globals.system_error = Alarms.ALARM_NAV350_CONNECTION_ERROR;

                        nav350_socket_open = false;
                    }
                    else
                    {
                        if (!nav350TransThread.IsAlive)
                        {
                            nav350TransThread = new Thread(NAVSensor.NAV_TransCheckThread);
                            nav350TransThread.Start();
                        }

                        if (!nav350RcvThread.IsAlive)
                        {
                            nav350RcvThread = new Thread(NAVSensor.NAV_RcvCheckThread);
                            nav350RcvThread.Start();
                        }

                        if (nav350TransThread.IsAlive && nav350RcvThread.IsAlive)
                        {
                            NAVSensorModel navSensorModel = new NAVSensorModel();
                            navSensorModel.naviX = Globals.nav_x;
                            navSensorModel.naviY = Globals.nav_y;
                            navSensorModel.naviT = Globals.nav_phi;

                            //navSensorModel.naviX = Globals.nav_y;
                            //navSensorModel.naviY = Globals.nav_x * -1;
                            //long adjustedT = Globals.nav_phi - 1800;
                            //adjustedT = -adjustedT;
                            //long normalizedT = (adjustedT + 3600) % 3600;
                            //navSensorModel.naviT = normalizedT;

                            navSensorModel.result = navMode;
                            //ZoneID Send
                            _eventAggregator.GetEvent<NAVSensorEvent>().Publish(navSensorModel);
                        }

                        // [NAV FREEZE CHECK] NAV 값이 변하지 않으면 카운트 증가, 변하면 리셋
                        if (Globals.nav_x == prevNavX && Globals.nav_y == prevNavY && Globals.nav_phi == prevNavT)
                        {
                            navFreezeCount++;
                        }
                        else
                        {
                            navFreezeCount = 0;
                            prevNavX = Globals.nav_x;
                            prevNavY = Globals.nav_y;
                            prevNavT = Globals.nav_phi;
                        }

                        // [NAV FREEZE CHECK] 일정 횟수 이상 변화 없으면 프로그램 셧다운
                        if (navFreezeCount >= navFreezeThreshold)
                        {
                            // 1) 내부 알람 상태 갱신(주기 타이머가 alarm_raise를 DB에 기록)
                            SysAlarm.AddErrorCodes(SysAlarm.LiDar2DFreeze);

                            // 2) 타이머가 DB에 기록할 수 있도록 짧게 대기
                            Thread.Sleep(400);

                            // 3) 종료
                            Tools.Log("NAV SENSOR FREEZE DETECTED. PROGRAM SHUTDOWN.", Tools.ELogType.NAVLog);
                            System.Windows.Application.Current.Shutdown();
                            return;
                        }
                    }
                }
                else
                {
                    nav350_socket_open = NAVSensor.NAV_SockConn(gIP, gPORT);
                    if (nav350_socket_open == true)
                    {
                        Globals.setTimerCounter(Globals.nav_rcv);
                        nav350TransThread = new Thread(NAVSensor.NAV_TransCheckThread);
                        nav350RcvThread = new Thread(NAVSensor.NAV_RcvCheckThread);
                    }
                }
                Tools.Log("alarm : " + Globals.system_error, Tools.ELogType.NAVLog);
                Thread.Sleep(100);
            }
        }

        public static bool NAV_SockConn(string IP, string PORT)
        {
            bool output = false;

            try
            {
                socketSend = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IAsyncResult result = socketSend.BeginConnect(IP, Convert.ToInt16(PORT), null, null);

                bool success = result.AsyncWaitHandle.WaitOne(1000, true);

                if (socketSend.Connected)
                {
                    socketSend.EndConnect(result);
                    output = true;
                    Tools.Log("Connected NAV350 to TCP", Tools.ELogType.NAVLog);
                    Globals.system_error = Alarms.ALARM_NONE;
                }
                else
                {
                    // NOTE, MUST CLOSE THE SOCKET
                    Globals.system_error = Alarms.ALARM_NAV350_CONNECTION_ERROR;
                    socketSend.Close();
                    socketSend.Dispose();
                    output = false;
                    throw new ApplicationException("Failed to connect NAV350.");
                }
            }
            catch (Exception ex)
            {
                Tools.Log(ex.ToString(), Tools.ELogType.NAVLog);
            }

            return output;
        }

        public static void NAV_TransCMD(byte cmd)
        {
            switch (cmd)
            {
                case (byte)NAV350_TRANSIT_CMD.CMD_SET_USER: NAV_SendCMD_LogIn(); break;
                case (byte)NAV350_TRANSIT_CMD.CMD_DATA_FORMAT: NAV_SendCMD_SetDataFormat(); break;
                case (byte)NAV350_TRANSIT_CMD.CMD_MODE_POWER_DOWN: NAV_SendCMD_Mode((byte)NAV350_MODE.MODE_POWER_DOWN); break;
                case (byte)NAV350_TRANSIT_CMD.CMD_MODE_STAND_BY: NAV_SendCMD_Mode((byte)NAV350_MODE.MODE_STAND_BY); break;
                case (byte)NAV350_TRANSIT_CMD.CMD_MODE_NAVI: NAV_SendCMD_Mode((byte)NAV350_MODE.MODE_NAVIGATION); break;
                case (byte)NAV350_TRANSIT_CMD.CMD_SET_LAYER: NAV_SendCMD_SetLayer(gNAVLayerCmd); break;
                case (byte)NAV350_TRANSIT_CMD.CMD_POSITION: NAV_SendCMD_GetPosition(); break;
                default: break;
            }

            gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_NONE;

            gNAVTransRetryCount = 0;
        }

        static void NAV_SendCMD_LogIn()
        {
            gNAVTranBuff = "02 73 4D 4E 20 53 65 74 41 63 63 65 73 73 4D 6F 64 65 20 33 20 46 34 37 32 34 37 34 34 03  ";
            byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
            socketSend.Send(buffer);
            Tools.Log("SendLogIn", Tools.ELogType.NAVLog);
        }

        // NAV_SendCMD_SetDataFormat
        static void NAV_SendCMD_SetDataFormat()
        {
            //{STX}sWN NPOSPoseDataFormat 1 1{ETX}

            gNAVTranBuff = "02 73 57 4E 20 4E 50 4F 53 50 6F 73 65 44 61 74 61 46 6F 72 6D 61 74 20 31 20 31 03 ";
            byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
            socketSend.Send(buffer);
            Tools.Log("SetDataFormat", Tools.ELogType.NAVLog);
        }

        // NAV_SendCMD_Mode
        static void NAV_SendCMD_Mode(byte mode)
        {
            //{STX}sMN mNEVAChangeState {mode}{ETX}
            gNAVTranBuff = "02 73 4D 4E 20 6D 4E 45 56 41 43 68 61 6E 67 65 53 74 61 74 65 20 " + Globals.byteToString(mode) + " 03 ";
            byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
            socketSend.Send(buffer);
            Tools.Log("SendMode", Tools.ELogType.NAVLog);
        }

        // NAV_SendCMD_SetLayer
        static void NAV_SendCMD_SetLayer(byte layer)
        {
            //{STX}sWN NEVACurrLayer {layer}{ETX}
            gNAVTranBuff = "02 73 57 4E 20 4E 45 56 41 43 75 72 72 4C 61 79 65 72 20 " + Globals.byteToString(layer) + " 03 ";
            byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
            socketSend.Send(buffer);
            Tools.Log("SetLayer", Tools.ELogType.NAVLog);
        }

        // NAV_SendCMD_GetPosition
        static void NAV_SendCMD_GetPosition()
        {
            //{STX}sMN mNPOSGetPose 1{ETX}
            gNAVTranBuff = "02 73 4d 4e 20 6d 4e 50 4f 53 47 65 74 50 6f 73 65 20 30 03  ";
            byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
            socketSend.Send(buffer);
        }
        public static void NAV_TransCheckThread()
        {
            while (true)
            {
                gNavTransErrorCheck = false;
                if ((Globals.system_error >= 1 && Globals.system_error <= 23) && Globals.system_error != 12)
                {
                    if (gResetCount >= 30)
                    {
                        NAV_SoftWareReset();
                        gResetCount = 0;

                        gNavTransErrorCheck = true;
                    }
                    else
                    {
                        gResetCount++;
                    }
                    //gNavTransErrorCheck = true;
                }

                if (gNavTransErrorCheck == false)
                {

                    if (gNAVstate.Equals((byte)NAV350_STATE.NAV_STATE_NONE))
                    {
                        gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_SET_USER;
                        gCopyBufferNavCmd = (byte)NAV350_TRANSIT_CMD.CMD_SET_USER;
                    }
                    else if (gNAVstate.Equals((byte)NAV350_STATE.NAV_STATE_LOGIN))
                    {
                        gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_MODE_STAND_BY;
                        gCopyBufferNavCmd = (byte)NAV350_TRANSIT_CMD.CMD_MODE_STAND_BY;
                    }
                    else if (gNAVstate.Equals((byte)NAV350_STATE.NAV_STATE_CHANGE_STAND_BY))
                    {
                        gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_SET_LAYER;
                        gCopyBufferNavCmd = (byte)NAV350_TRANSIT_CMD.CMD_SET_LAYER;
                    }
                    else if (gNAVstate.Equals((byte)NAV350_STATE.NAV_STATE_SET_LAYER))
                    {
                        gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_DATA_FORMAT;
                        gCopyBufferNavCmd = (byte)NAV350_TRANSIT_CMD.CMD_DATA_FORMAT;
                    }
                    else if (gNAVstate.Equals((byte)NAV350_STATE.NAV_STATE_SET_DATA_FORMAT))
                    {
                        gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_MODE_NAVI;
                        gCopyBufferNavCmd = (byte)NAV350_TRANSIT_CMD.CMD_MODE_NAVI;
                    }
                    else if (gNAVstate.Equals((byte)NAV350_STATE.NAV_STATE_CHANGE_NAVIGATION))
                    {
                        gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION;
                        gCopyBufferNavCmd = (byte)NAV350_TRANSIT_CMD.CMD_POSITION;
                    }

                    Tools.Log("gNAVCommand : " + gNAVCommand, Tools.ELogType.NAVLog);
                    if (gNAVCommand != (byte)NAV350_TRANSIT_CMD.CMD_NONE)
                    {
                        NAV_TransCMD(gNAVCommand);
                    }

                    else
                    {
                        if (gNAVTransTimeOutCount <= 20)
                        {
                            gNAVTransTimeOutCount++;
                        }
                        else  // (gNAVCommandNew == CMD_NONE) && (gNAVTransTimeOutCount > 20)
                        {
                            gNAVTransTimeOutCount = 0;
                            gNAVTransRetryCount++;

                            if (gNAVTransRetryCount >= 5)
                            {
                                if ((gNAVTransRetryCount % 5) == 0)  //  Transmit Command Retry 
                                {
                                    NAV_TransCMD(gNAVCommand);
                                    Tools.Log("Retry NAV ", Tools.ELogType.NAVLog);
                                }

                                if (gNAVTransRetryCount >= 15) // NAV350 SoftWare Reset
                                {
                                    //Globals.system_error = Alarms.ALARM_NAV350_TRANSMIT_ERROR;
                                    NAV_SoftWareReset();
                                    Tools.Log("Reset NAV ", Tools.ELogType.NAVLog);
                                }
                            }
                        }
                    }
                }
                Thread.Sleep(127);
            }
        }

        static void NAV_RcvCMD_LogIn(string[] cmd_type, int length)
        {
            if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sAN"))
            {
                if (cmd_type[(int)NAV350_RCV_INDEX.nLogin.mSuccess].Equals("1"))
                {
                    //NAV_SetCMD((byte)NAV350_CMD.CMD_DATA_FORMAT);
                    gNAVstate = (byte)NAV350_STATE.NAV_STATE_LOGIN;
                    gTranscheck = 0;
                    Tools.Log("LogIn_OK NAV ", Tools.ELogType.NAVLog);
                }
                else
                    Globals.system_error = Alarms.ALARM_NAV350_LOGIN;
            }
        }

        static void NAV_RcvCMD_Data_Format(string[] cmd_type, int length)
        {
            if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sWA"))
            {
                gNAVstate = (byte)NAV350_STATE.NAV_STATE_SET_DATA_FORMAT;
                gTranscheck = 0;
            }

            else if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sRN"))
            {
                gNAVstate = (byte)NAV350_STATE.NAV_STATE_SET_DATA_FORMAT;
                gTranscheck = 0;
            }

            else if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sRA"))
            {
                gNAVstate = (byte)NAV350_STATE.NAV_STATE_SET_DATA_FORMAT;
                gTranscheck = 0;
            }

            else
                Globals.system_error = Alarms.ALARM_NAV350_SET_DATA_FORMAT;
        }

        static void NAV_RcvCMD_Layer(string[] cmd_type, int index)
        {
            if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sRA"))
            {
                if (gNAVLayerCmd.Equals((byte)NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.currLayer_CurrLayer])))
                    gNAVstate = (byte)NAV350_STATE.NAV_STATE_SET_LAYER;

                gTranscheck = 0;
            }
            else if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sWA"))
            {
                Tools.Log("sWALayer_OK NAV ", Tools.ELogType.NAVLog);
                gNAVstate = (byte)NAV350_STATE.NAV_STATE_SET_LAYER;
                gTranscheck = 0;
            }
            else if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sRN"))
            {
                gNAVstate = (byte)NAV350_STATE.NAV_STATE_SET_LAYER;
                gTranscheck = 0;
            }
            else
                Globals.system_error = Alarms.ALARM_NAV350_SET_LAYER;
        }
        static void NAV_RcvCMD_Mode(string[] cmd_type, int length)
        {
            if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sAN"))
            {
                if (!cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mErrorCode].Equals("0")) // 0 : no Error
                {
                    if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mErrorCode].Equals("1"))
                        Globals.system_error = Alarms.ALARM_NAV350_MODE_CHANGE_INVALID_CHANGE;
                    else if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mErrorCode].Equals("2"))
                        Globals.system_error = Alarms.ALARM_NAV350_MODE_CHANGE_METHOD_BREAK;
                    else if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mErrorCode].Equals("3"))
                        Globals.system_error = Alarms.ALARM_NAV350_MODE_CHANGE_UNKNOWN_OP_MODE;
                    else if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mErrorCode].Equals("5"))
                        Globals.system_error = Alarms.ALARM_NAV350_MODE_CHANGE_TIMEOUT;
                    else if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mErrorCode].Equals("6"))
                        Globals.system_error = Alarms.ALARM_NAV350_MODE_CHANGE_ANOTHER_CMD_ACTIVE;
                    else if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mErrorCode].Equals("7"))
                        Globals.system_error = Alarms.ALARM_NAV350_MODE_CHANGE_GENERAL_ERROR;

                    return;
                }

                if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mMode].Equals("0"))
                {
                    gNAVstate = (byte)NAV350_STATE.NAV_STATE_CHANGE_POWER_DOWN;
                    gTranscheck = 0;
                    Tools.Log("PowerDown OK ", Tools.ELogType.NAVLog);
                }
                else if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mMode] == "1")
                {
                    gNAVstate = (byte)NAV350_STATE.NAV_STATE_CHANGE_STAND_BY;
                    gTranscheck = 0;
                    Tools.Log("StandBy OK ", Tools.ELogType.NAVLog);
                }
                else if (cmd_type[(int)NAV350_RCV_INDEX.nRcvCmdMode.mMode].Equals("4"))
                {
                    gNAVstate = (byte)NAV350_STATE.NAV_STATE_CHANGE_NAVIGATION;
                    gTranscheck = 0;
                    Tools.Log("NAV OK ", Tools.ELogType.NAVLog);
                }
            }
        }

        static string navMode;
        static void NAV_RcvCMD_Position(string[] cmd_type, int index)
        {
            navMode = "";
            byte ErrCode = 0;
            if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sAN"))
            {
                gNAVPosCount++;

                if (!cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mErrorCode].Equals("0"))
                { // 0: no Error
                    ErrCode = (byte)NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mErrorCode]);


                    switch (ErrCode)
                    {
                        case 1: Globals.system_error = Alarms.ALARM_NAV350_POSE_WRONG_OP_MODE; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break; //wrong operating mode
                        case 2: Globals.system_error = Alarms.ALARM_NAV350_POSE_ASYNC_TERMINATED; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break; //asynchrony Method terminated
                        case 3: Globals.system_error = Alarms.ALARM_NAV350_POSE_INVALID_DATA; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break; //invalid data
                        case 4: Globals.system_error = Alarms.ALARM_NAV350_POSE_NO_POS_AVAILABLE; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break; //no position available
                        case 5: Globals.system_error = Alarms.ALARM_NAV350_POSE_TIMEOUT; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break; //timeout
                        case 6: Globals.system_error = Alarms.ALARM_NAV350_POSE_METHOD_ALREADY_ACTIVE; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break; //method already active
                        case 7: Globals.system_error = Alarms.ALARM_NAV350_POSE_GENERAL_ERROR; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break; //general error
                    }

                    return;
                }

                if (positionNavCount > 10 && cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mNavMode] == "1")
                {
                    Globals.nav_x = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mXPos]);
                    Globals.nav_y = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mYPos]);
                    Globals.nav_phi = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mPhi]) / 100;
                    Globals.nav_dev = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mMeanDev]);
                }

                // Position Mode
                ErrCode = (byte)NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mInfoState]);
                navMode = cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mNavMode];

                //Tools.Log("Mode: "+ cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mNavMode] + " ErrCode: " + ErrCode, Tools.ELogType.BackEndLog);


                if ((ErrCode > 1))
                    positionErrCount++;
                else
                    positionErrCount = 0;

                if (positionErrCount > 30)
                {
                    Globals.system_error = Alarms.ALARM_NAV350_POSE_UNKNOWN_ERROR;
                }
                else
                {
                    gNAVstate = (byte)NAV350_STATE.NAV_STATE_GET_POS;
                    gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION;
                    Globals.system_error = Alarms.ALARM_NONE;
                    gTranscheck = 0;
                }
                positionNavCount++;
                if (positionNavCount > 65533)
                    positionNavCount = 9;
            }

        }

        static bool NAV_CopyRcvBuffer(out string[] cmd, string rcvData, int length)
        {
            cmd = null;
            string rcvDataTemp = string.Empty;
            if (rcvData[0] == '')
            {
                if (rcvData[length - 1] == '')
                {
                    cmd = new string[length];
                    rcvDataTemp = string.Join("", rcvData.Split('', ''));
                    cmd = rcvDataTemp.Split(' ');
                    return true;
                }
            }
            return false;
        }

        static void NAV_ClearRcvBuffer(ref string[] cmd, ref string rcvData, ref int length)
        {
            Array.Clear(cmd, 0, cmd.Length);
            rcvData = string.Empty;
            length = 0;
        }

        public static void NAV_RcvCheckThread()
        {
            while (true)
            {
                int ReceiveDataSize = 0;
                string ReceiveData = string.Empty;
                string[] cmd = { };
                gNavRcvErrorCheck = false;


                ReceiveDataSize = socketSend.Available;
                byte[] buffer = new byte[ReceiveDataSize];
                if (ReceiveDataSize > 0)
                {
                    socketSend.Receive(buffer);
                    ReceiveData = Encoding.UTF8.GetString(buffer, 0, ReceiveDataSize);
                    bool CopyError = NAV_CopyRcvBuffer(out cmd, ReceiveData, ReceiveDataSize);
                    if (!CopyError)
                    {
                        gNavRcvErrorCheck = true;
                    }
                    int cmd_length = cmd.Length;


                    if (cmd[0].Equals("sFA"))
                    {
                        Globals.system_error = Alarms.ALARM_NAV350_COMM_CMD_ERROR;
                        NAV_ClearRcvBuffer(ref cmd, ref ReceiveData, ref ReceiveDataSize);
                        gNavRcvErrorCheck = true;
                    }
                    if (cmd_length < 2)
                    {
                        Globals.system_error = Alarms.ALARM_NAV350_COMM_INDEX_ERROR;
                        gNavRcvErrorCheck = true;
                    }

                    //gTranscheck = 0;
                    if (gNavRcvErrorCheck == false)
                    {
                        if (cmd[NAV350_RCV_INDEX.pub_Cmd].Equals("SetAccessMode"))
                        {
                            NAV_RcvCMD_LogIn(cmd, cmd_length);
                        }
                        else if (cmd[NAV350_RCV_INDEX.pub_Cmd].Equals("mNEVAChangeState"))
                        {
                            NAV_RcvCMD_Mode(cmd, cmd_length);
                        }
                        else if (cmd[NAV350_RCV_INDEX.pub_Cmd].Equals("NPOSPoseDataFormat"))
                        {
                            NAV_RcvCMD_Data_Format(cmd, cmd_length);
                        }
                        else if (cmd[NAV350_RCV_INDEX.pub_Cmd].Equals("NEVACurrLayer"))
                        {
                            NAV_RcvCMD_Layer(cmd, cmd_length);
                        }
                        else if (cmd[NAV350_RCV_INDEX.pub_Cmd].Equals("mNPOSGetPose"))
                        {
                            Tools.Log("buffer: " + ReceiveData, Tools.ELogType.NAVLog);
                            NAV_RcvCMD_Position(cmd, cmd_length);
                        }
                    }
                    Globals.setTimerCounter(Globals.nav_rcv);
                }// end ReceiveData Not NULL

            }
        }

        public static void NAV_SoftWareReset()
        {
            // Variable Init
            //NAV_ClearRcvBuffer();

            gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_NONE;

            gNAVTransTimeOutCount = 0;
            gNAVTransRetryCount = 0;

            gResetCount = 0;

            gNAVPosCount = 0;

            gNAVstate = (byte)NAV350_STATE.NAV_STATE_NONE;
            gTranscheck = 2;
            gCopyBufferNavCmd = 0;
            positionErrCount = 0;
            positionNavCount = 0;
            Globals.system_error = Alarms.ALARM_NAV350_RESET;
        }


        static int NAV_StringToInt(string sbuff)
        {
            int i, j, size = 0;
            int num, tmp;
            int data;

            char[] buff = sbuff.ToCharArray();

            num = 0;

            // Find Size
            for (i = 0; i < buff.Length; i++)
            {
                if (buff[i] == '\0')
                {
                    size = i;

                    break;
                }
            }

            // Trans Data (hex to int)
            for (i = buff.Length - 1, j = 0; i >= 0; i--, j++)
            {
                data = buff[i];

                if ((data >= '0') && (data <= '9'))
                {
                    tmp = (int)(data - 0x30);
                }
                else if ((data >= 'a') && (data <= 'f'))
                {
                    tmp = (int)(data - 87);
                }
                else if ((data >= 'A') && (data <= 'F'))
                {
                    tmp = (int)(data - 55);
                }
                else
                {
                    tmp = 0;
                }

                num = num | (tmp << (j * 4));
            }
            return num;
        }
    }
}
enum NAV350_STATE : byte
{
    NAV_STATE_NONE = 0,
    NAV_STATE_LOGIN,
    NAV_STATE_CHANGE_POWER_DOWN,
    NAV_STATE_CHANGE_STAND_BY,
    NAV_STATE_CHANGE_NAVIGATION,
    NAV_STATE_SET_LAYER,
    NAV_STATE_SET_DATA_FORMAT,
    NAV_STATE_GET_POS,
}
// NAV350 CMD
enum NAV350_TRANSIT_CMD : byte
{
    CMD_NONE = 0,
    CMD_SET_USER,
    CMD_DATA_FORMAT,
    CMD_MODE_POWER_DOWN,
    CMD_MODE_STAND_BY,
    CMD_MODE_NAVI,
    CMD_SET_LAYER,
    CMD_POSITION
}
// NAV350 Command Mode
enum NAV350_MODE : byte
{
    MODE_POWER_DOWN = 0,
    MODE_STAND_BY,
    MODE_MAPPING,
    MODE_LAYER_DETECT,
    MODE_NAVIGATION
}

struct NAV350_RCV_INDEX
{
    public const int pub_CmdType = 0;
    public const int pub_Cmd = 1;
    public const int currLayer_CurrLayer = 2;
    public enum nLogin
    {
        mSuccess = 2
    }
    public enum nRcvCmdMode
    {
        mErrorCode = 2,
        mMode
    }
    public enum nCurrLayer
    {
        mcurrLayer = 2
    }
    public enum nPosGet
    {
        mVersion = 2,
        mErrorCode,
        mWait,
        mPoseData,
        mXPos,
        mYPos,
        mPhi,
        mOptPose,
        mOutMode,
        mTimeStamp,
        mMeanDev,
        mNavMode,
        mInfoState,
        mNumUsedReflectors
    }
};