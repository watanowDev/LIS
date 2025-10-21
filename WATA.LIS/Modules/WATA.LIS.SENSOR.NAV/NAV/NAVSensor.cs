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
using WATA.LIS.Core.Events.System;

namespace WATA.LIS.SENSOR.NAV
{
    public class NAVSensor : IDisposable
    {
        Thread RecvThread;

        private readonly IEventAggregator _eventAggregator;
        public static Socket socketSend;

        // 스레드 안전성을 위한 lock 객체들
        private static readonly object _socketLock = new object();
        private static readonly object _stateLock = new object();

        // 스레드 종료를 위한 CancellationToken
        private static CancellationTokenSource _cancellationTokenSource;
        private static CancellationToken _cancellationToken;

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

        // NAV pose filtering state
        private static readonly int PoseWarmupFrames = 1;
        private static long lastGoodX = 0;
        private static long lastGoodY = 0;
        private static long lastGoodT = 0;
        private static bool hasLastGood = false;

        public bool nav350_socket_open = false;
        bool nav350_socket_open_once = false;
        Thread nav350TransThread;
        Thread nav350RcvThread;
        System.Threading.Timer countTimer;
        Thread mainProcess;

        private static string gIP = "169.254.4.63";
        private static string gPORT = "2111";

        // 2단계 셧다운 정책을 위한 상태 추적 변수
        private static bool isReconnecting = false;
        private static DateTime reconnectStartTime;
        private static int postReconnectFreezeCount = 0;
        private const int postReconnectThreshold = 150;

        // 로그 중복 방지를 위한 변수
        private static int lastLoggedError = -1;

        // Dispose 플래그
        private bool _disposed = false;

        // 설정 값들 (설정 파일에서 로드)
        private static int _freezeThreshold = 300;
        private static int _transTimeoutCount = 20;
        private static int _transRetryMax = 15;
        private static int _reconnectTimeoutSeconds = 5;
        private static int _socketConnectTimeoutMs = 1000;
        private static int _maxReceiveBufferSize = 4096;

        public NAVSensor(IEventAggregator eventAggregator, INAVModel navModel)
        {
            _eventAggregator = eventAggregator;

            NAVConfigModel _navConfig = (NAVConfigModel)navModel;
            gIP = _navConfig.IP;
            gPORT = _navConfig.PORT.ToString();

            // 설정 값 로드 (JSON에 값이 없으면 기본값 사용)
            _freezeThreshold = _navConfig.FreezeThreshold;
            _transTimeoutCount = _navConfig.TransTimeoutCount;
            _transRetryMax = _navConfig.TransRetryMax;
            _reconnectTimeoutSeconds = _navConfig.ReconnectTimeoutSeconds;
            _socketConnectTimeoutMs = _navConfig.SocketConnectTimeoutMs;
            _maxReceiveBufferSize = _navConfig.MaxReceiveBufferSize;

            Tools.Log($"NAV Config loaded: Freeze={_freezeThreshold}, TransTimeout={_transTimeoutCount}, " +
                      $"RetryMax={_transRetryMax}, ReconnectTimeout={_reconnectTimeoutSeconds}s, " +
                      $"SocketTimeout={_socketConnectTimeoutMs}ms, MaxBuffer={_maxReceiveBufferSize}", 
                      Tools.ELogType.SystemLog);

            // CancellationTokenSource 초기화
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        public void Init()
        {
            Tools.Log($"Init NAV", Tools.ELogType.SystemLog);

            mainProcess = new Thread(Main);
            mainProcess.IsBackground = true; // 백그라운드 스레드로 설정
            mainProcess.Start();
        }

        public void Main()
        {
            long prevNavX = Globals.nav_x;
            long prevNavY = Globals.nav_y;
            long prevNavT = Globals.nav_phi;
            int navFreezeCount = 0;
            int navFreezeThreshold = _freezeThreshold; // 설정 파일에서 로드된 값 사용

            // 오탐 방지를 위한 연속 Freeze 카운트
            int consecutiveFreezeCount = 0;
            const int minConsecutiveFreezes = 2; // 2회 연속 확인

            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    if (nav350_socket_open == true)
                    {
                        if (nav350_socket_open_once == false)
                        {
                            nav350TransThread = new Thread(NAVSensor.NAV_TransCheckThread);
                            nav350TransThread.IsBackground = true;
                            nav350TransThread.Start();

                            nav350RcvThread = new Thread(NAVSensor.NAV_RcvCheckThread);
                            nav350RcvThread.IsBackground = true;
                            nav350RcvThread.Start();

                            nav350_socket_open_once = true;
                        }

                        if (Globals.getTimerCounter(Globals.nav_rcv) == 0) // NAV350 통신 연결 상태 확인
                        {
                            if (nav350_socket_open_once == true)
                            {
                                nav350_socket_open_once = false;
                            }

                            // 소켓 정리 시 lock 사용
                            lock (_socketLock)
                            {
                                try
                                {
                                    if (socketSend != null)
                                    {
                                        if (socketSend.Connected)
                                        {
                                            socketSend.Shutdown(SocketShutdown.Both);
                                        }
                                        socketSend.Close();
                                        socketSend.Dispose();
                                        socketSend = null;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Tools.Log($"Socket cleanup error: {ex.Message}", Tools.ELogType.SystemLog);
                                }
                            }

                            NAVSensor.NAV_SoftWareReset();

                            Globals.system_error = Alarms.ALARM_NAV350_CONNECTION_ERROR;

                            nav350_socket_open = false;
                        }
                        else
                        {
                            if (!nav350TransThread.IsAlive)
                            {
                                nav350TransThread = new Thread(NAVSensor.NAV_TransCheckThread);
                                nav350TransThread.IsBackground = true;
                                nav350TransThread.Start();
                            }

                            if (!nav350RcvThread.IsAlive)
                            {
                                nav350RcvThread = new Thread(NAVSensor.NAV_RcvCheckThread);
                                nav350RcvThread.IsBackground = true;
                                nav350RcvThread.Start();
                            }

                            if (nav350TransThread.IsAlive && nav350RcvThread.IsAlive)
                            {
                                NAVSensorModel navSensorModel = new NAVSensorModel();
                                navSensorModel.naviX = Globals.nav_x;
                                navSensorModel.naviY = Globals.nav_y;
                                navSensorModel.naviT = Globals.nav_phi;

                                navSensorModel.result = navMode;

                                // 정상 동작 시 에러 코드 제거
                                SysAlarm.RemoveErrorCodes(SysAlarm.LiDar2DConnErr);
                                SysAlarm.RemoveErrorCodes(SysAlarm.LiDar2DFreeze);

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
                                consecutiveFreezeCount = 0; // ⭐ 오탐 방지: 값이 변하면 연속 카운트 리셋
                                prevNavX = Globals.nav_x;
                                prevNavY = Globals.nav_y;
                                prevNavT = Globals.nav_phi;
                            }

                            // [NAV FREEZE CHECK] Freeze 감지 (오탐 방지 로직 추가)
                            if (navFreezeCount >= navFreezeThreshold)
                            {
                                consecutiveFreezeCount++; // ⭐ Freeze 임계값 도달 시 연속 카운트 증가

                                // ⭐ 2회 연속 Freeze 확인 후에만 재연결 시도
                                if (consecutiveFreezeCount >= minConsecutiveFreezes)
                                {
                                    // 1단계: Unknown 에러 + Freeze 시 재연결 시도
                                    if (!isReconnecting && Globals.system_error == Alarms.ALARM_NAV350_POSE_UNKNOWN_ERROR)
                                    {
                                        Tools.Log("NAV SENSOR: Unknown error + freeze detected (2 consecutive). Attempting socket reconnection...", Tools.ELogType.SystemLog);

                                        SysAlarm.AddErrorCodes(SysAlarm.LiDar2DConnErr);

                                        if (nav350_socket_open_once == true)
                                        {
                                            nav350_socket_open_once = false;
                                        }

                                        NAVSensor.NAV_SoftWareReset();

                                        Thread.Sleep(500); // ⚡ 초기화 대기 시간 단축 (1000 → 500)

                                        nav350_socket_open = false;

                                        isReconnecting = true;
                                        reconnectStartTime = DateTime.Now;
                                        postReconnectFreezeCount = 0;
                                        navFreezeCount = 0;
                                        consecutiveFreezeCount = 0; // ⭐ 리셋

                                        Tools.Log("NAV SENSOR: Socket reconnection initiated.", Tools.ELogType.SystemLog);
                                    }
                                    // 2단계: 일반 Freeze 상태에서도 재연결 시도
                                    else if (!isReconnecting)
                                    {
                                        Tools.Log("NAV SENSOR FREEZE DETECTED (2 consecutive): Attempting socket reconnection before shutdown...", Tools.ELogType.SystemLog);

                                        SysAlarm.AddErrorCodes(SysAlarm.LiDar2DFreeze);

                                        // 소켓 강제 재연결 시도
                                        if (nav350_socket_open_once == true)
                                        {
                                            nav350_socket_open_once = false;
                                        }

                                        // 소켓 정리
                                        lock (_socketLock)
                                        {
                                            try
                                            {
                                                if (socketSend != null)
                                                {
                                                    if (socketSend.Connected)
                                                    {
                                                        socketSend.Shutdown(SocketShutdown.Both);
                                                    }
                                                    socketSend.Close();
                                                    socketSend.Dispose();
                                                    socketSend = null;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Tools.Log($"Socket cleanup during freeze recovery: {ex.Message}", Tools.ELogType.SystemLog);
                                            }
                                        }

                                        NAVSensor.NAV_SoftWareReset();

                                        Thread.Sleep(500); // ⚡ 초기화 대기 시간 단축 (1000 → 500)

                                        nav350_socket_open = false;

                                        // 재연결 상태 설정
                                        isReconnecting = true;
                                        reconnectStartTime = DateTime.Now;
                                        postReconnectFreezeCount = 0;
                                        navFreezeCount = 0;
                                        consecutiveFreezeCount = 0; // ⭐ 리셋

                                        Tools.Log("NAV SENSOR: Freeze recovery - Socket reconnection initiated.", Tools.ELogType.SystemLog);
                                    }
                                }
                            }
                            else
                            {
                                // ⭐ Freeze 임계값 미만이면 연속 카운트 리셋
                                consecutiveFreezeCount = 0;
                            }

                            // 재연결 후 상태 모니터링
                            if (isReconnecting)
                            {
                                // 재연결 후 설정된 시간 경과 체크
                                if ((DateTime.Now - reconnectStartTime).TotalSeconds > _reconnectTimeoutSeconds)
                                {
                                    if (navFreezeCount > 0 || Globals.system_error == Alarms.ALARM_NAV350_POSE_UNKNOWN_ERROR)
                                    {
                                        Tools.Log("NAV SENSOR: Reconnection failed. Post-reconnect issues persist. PROGRAM SHUTDOWN - DISABLED.", Tools.ELogType.SystemLog);

                                        SysAlarm.AddErrorCodes(SysAlarm.LiDar2DFreeze);

                                        _eventAggregator.GetEvent<ShutdownEngineEvent>().Publish();

                                        Thread.Sleep(400);
                                    }
                                    else
                                    {
                                        Tools.Log("NAV SENSOR: Reconnection successful. Normal operation resumed.", Tools.ELogType.SystemLog);

                                        // ⭐ 재연결 성공 시 모든 상태 리셋
                                        Globals.system_error = Alarms.ALARM_NONE;
                                        SysAlarm.RemoveErrorCodes(SysAlarm.LiDar2DFreeze);
                                        SysAlarm.RemoveErrorCodes(SysAlarm.LiDar2DConnErr);

                                        isReconnecting = false;
                                        postReconnectFreezeCount = 0;
                                        consecutiveFreezeCount = 0; // ⭐ 리셋
                                    }
                                }
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
                            nav350TransThread.IsBackground = true;
                            nav350RcvThread = new Thread(NAVSensor.NAV_RcvCheckThread);
                            nav350RcvThread.IsBackground = true;

                            // 재연결 상태에서 소켓 연결 성공 시 로그 출력
                            if (isReconnecting)
                            {
                                Tools.Log("NAV SENSOR: Socket reconnection established successfully.", Tools.ELogType.SystemLog);
                            }
                        }
                    }

                    // 로그 중복 방지
                    if (Globals.system_error != lastLoggedError)
                    {
                        lastLoggedError = Globals.system_error;
                    }

                    Thread.Sleep(100);
                }
            }
            catch (OperationCanceledException)
            {
                Tools.Log("NAV Main thread cancelled gracefully", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"NAV Main thread error: {ex.Message}", Tools.ELogType.SystemLog);
            }
            finally
            {
                Tools.Log("NAV Main thread exiting", Tools.ELogType.SystemLog);
            }
        }

        public static bool NAV_SockConn(string IP, string PORT)
        {
            bool output = false;

            lock (_socketLock)
            {
                try
                {
                    socketSend = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IAsyncResult result = socketSend.BeginConnect(IP, Convert.ToInt16(PORT), null, null);

                    // 설정된 타임아웃 사용 (기본 1000ms)
                    bool success = result.AsyncWaitHandle.WaitOne(_socketConnectTimeoutMs, true);

                    if (socketSend.Connected)
                    {
                        socketSend.EndConnect(result);
                        output = true;
                        Tools.Log("Connected NAV350 to TCP", Tools.ELogType.SystemLog);
                        Globals.system_error = Alarms.ALARM_NONE;

                        SysAlarm.RemoveErrorCodes(SysAlarm.LiDar2DConnErr);
                    }
                    else
                    {
                        Globals.system_error = Alarms.ALARM_NAV350_CONNECTION_ERROR;
                        socketSend.Close();
                        socketSend.Dispose();
                        socketSend = null;
                        output = false;
                        throw new ApplicationException("Failed to connect NAV350.");
                    }
                }
                catch (SocketException sex)
                {
                    Tools.Log($"Socket connection failed: {sex.SocketErrorCode} - {sex.Message}", Tools.ELogType.SystemLog);
                    if (socketSend != null)
                    {
                        try { socketSend.Dispose(); } catch { }
                        socketSend = null;
                    }
                }
                catch (Exception ex)
                {
                    Tools.Log($"Unexpected connection error: {ex.Message}", Tools.ELogType.SystemLog);
                    if (socketSend != null)
                    {
                        try { socketSend.Dispose(); } catch { }
                        socketSend = null;
                    }
                }
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
            lock (_socketLock)
            {
                if (socketSend != null && socketSend.Connected)
                {
                    // 수정 전 (오타): 41 43 43 → ACC
                    // gNAVTranBuff = "02 73 4D 4E 20 53 65 74 41 43 43 65 73 73 4D 6F 64 65 20 33 20 46 34 37 32 34 37 34 34 03  ";

                    // 수정 후 (올바름): 41 63 63 → Acc
                    gNAVTranBuff = "02 73 4D 4E 20 53 65 74 41 63 63 65 73 73 4D 6F 64 65 20 33 20 46 34 37 32 34 37 34 34 03  ";

                    byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
                    try
                    {
                        socketSend.Send(buffer);
                        Tools.Log($"Sent Login Command: {gNAVTranBuff}", Tools.ELogType.SystemLog);
                    }
                    catch (Exception ex)
                    {
                        Tools.Log($"SendLogIn error: {ex.Message}", Tools.ELogType.SystemLog);
                    }
                }
            }
        }

        // NAV_SendCMD_SetDataFormat
        static void NAV_SendCMD_SetDataFormat()
        {
            lock (_socketLock)
            {
                if (socketSend != null && socketSend.Connected)
                {
                    gNAVTranBuff = "02 73 57 4E 20 4E 50 4F 53 50 6F 73 65 44 61 74 61 46 6F 72 6D 61 74 20 31 20 31 03 ";
                    byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
                    try
                    {
                        socketSend.Send(buffer);
                    }
                    catch (Exception ex)
                    {
                        Tools.Log($"SetDataFormat error: {ex.Message}", Tools.ELogType.SystemLog);
                    }
                }
            }
        }

        // NAV_SendCMD_Mode
        static void NAV_SendCMD_Mode(byte mode)
        {
            lock (_socketLock)
            {
                if (socketSend != null && socketSend.Connected)
                {
                    gNAVTranBuff = "02 73 4D 4E 20 6D 4E 45 56 41 43 68 61 6E 67 65 53 74 61 74 65 20 " + Globals.byteToString(mode) + " 03 ";
                    byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
                    try
                    {
                        socketSend.Send(buffer);
                    }
                    catch (Exception ex)
                    {
                        Tools.Log($"SendMode error: {ex.Message}", Tools.ELogType.SystemLog);
                    }
                }
            }
        }

        // NAV_SendCMD_SetLayer
        static void NAV_SendCMD_SetLayer(byte layer)
        {
            lock (_socketLock)
            {
                if (socketSend != null && socketSend.Connected)
                {
                    gNAVTranBuff = "02 73 57 4E 20 4E 45 56 41 43 75 72 72 4C 61 79 65 72 20 " + Globals.byteToString(layer) + " 03 ";
                    byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
                    try
                    {
                        socketSend.Send(buffer);
                    }
                    catch (Exception ex)
                    {
                        Tools.Log($"SetLayer error: {ex.Message}", Tools.ELogType.SystemLog);
                    }
                }
            }
        }

        // NAV_SendCMD_GetPosition
        static void NAV_SendCMD_GetPosition()
        {
            lock (_socketLock)
            {
                if (socketSend != null && socketSend.Connected)
                {
                    gNAVTranBuff = "02 73 4d 4e 20 6d 4e 50 4f 53 47 65 74 50 6f 73 65 20 30 03  ";
                    byte[] buffer = Globals.strToHexByte(gNAVTranBuff);
                    try
                    {
                        socketSend.Send(buffer);
                    }
                    catch (Exception ex)
                    {
                        Tools.Log($"GetPosition error: {ex.Message}", Tools.ELogType.SystemLog);
                    }
                }
            }
        }
        public static void NAV_TransCheckThread()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
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
                    }

                    if (gNavTransErrorCheck == false)
                    {
                        lock (_stateLock)
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
                        }

                        if (gNAVCommand != (byte)NAV350_TRANSIT_CMD.CMD_NONE)
                        {
                            NAV_TransCMD(gNAVCommand);
                        }
                        else
                        {
                            if (gNAVTransTimeOutCount <= _transTimeoutCount) // 설정 값 사용
                            {
                                gNAVTransTimeOutCount++;
                            }
                            else
                            {
                                gNAVTransTimeOutCount = 0;
                                gNAVTransRetryCount++;

                                if (gNAVTransRetryCount >= 250)
                                {
                                    Tools.Log("NAV TransRetryCount overflow prevention reset", Tools.ELogType.SystemLog);
                                    gNAVTransRetryCount = (byte)_transRetryMax; // 설정 값으로 리셋
                                }

                                if (gNAVTransRetryCount >= _transRetryMax) // 설정 값 사용
                                {
                                    NAV_SoftWareReset();
                                    Tools.Log("Reset NAV ", Tools.ELogType.SystemLog);
                                    gNAVTransRetryCount = 0;
                                }
                                else if (gNAVTransRetryCount >= 5 && (gNAVTransRetryCount % 5) == 0)
                                {
                                    NAV_TransCMD(gNAVCommand);
                                    Tools.Log("Retry NAV ", Tools.ELogType.SystemLog);
                                }
                            }
                        }
                    }

                    // CancellationToken을 지원하는 Sleep 사용
                    _cancellationToken.WaitHandle.WaitOne(127);
                }
            }
            catch (OperationCanceledException)
            {
                Tools.Log("NAV TransCheckThread cancelled gracefully", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"TransCheckThread error: {ex.Message}", Tools.ELogType.SystemLog);
            }
            finally
            {
                Tools.Log("NAV TransCheckThread exiting", Tools.ELogType.SystemLog);
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

            try
            {
                // 배열 길이 검증
                if (cmd_type == null || cmd_type.Length < (int)NAV350_RCV_INDEX.nPosGet.mMeanDev + 1)
                {
                    Tools.Log($"Position command array too short: {cmd_type?.Length ?? 0}", Tools.ELogType.SystemLog);
                    return;
                }

                if (cmd_type[NAV350_RCV_INDEX.pub_CmdType].Equals("sAN"))
                {
                    gNAVPosCount++;

                    if (!cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mErrorCode].Equals("0"))
                    {
                        ErrCode = (byte)NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mErrorCode]);

                        switch (ErrCode)
                        {
                            case 1: Globals.system_error = Alarms.ALARM_NAV350_POSE_WRONG_OP_MODE; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break;
                            case 2: Globals.system_error = Alarms.ALARM_NAV350_POSE_ASYNC_TERMINATED; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break;
                            case 3: Globals.system_error = Alarms.ALARM_NAV350_POSE_INVALID_DATA; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break;
                            case 4: Globals.system_error = Alarms.ALARM_NAV350_POSE_NO_POS_AVAILABLE; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break;
                            case 5: Globals.system_error = Alarms.ALARM_NAV350_POSE_TIMEOUT; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break;
                            case 6: Globals.system_error = Alarms.ALARM_NAV350_POSE_METHOD_ALREADY_ACTIVE; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break;
                            case 7: Globals.system_error = Alarms.ALARM_NAV350_POSE_GENERAL_ERROR; gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION; break;
                        }

                        return;
                    }

                    // Position Mode
                    ErrCode = (byte)NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mInfoState]);
                    navMode = cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mNavMode];

                    if ((ErrCode > 1))
                        positionErrCount++;
                    else
                        positionErrCount = 0;

                    if (positionErrCount > 5)
                    {
                        Globals.system_error = Alarms.ALARM_NAV350_POSE_UNKNOWN_ERROR;
                    }
                    else
                    {
                        // 후보 값 계산 - 유효성 검증 추가
                        if (string.IsNullOrEmpty(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mXPos]) ||
                            string.IsNullOrEmpty(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mYPos]) ||
                            string.IsNullOrEmpty(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mPhi]))
                        {
                            Tools.Log("Invalid position data received (empty fields)", Tools.ELogType.SystemLog);
                            return;
                        }

                        int candX = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mXPos]);
                        int candY = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mYPos]);
                        int candT = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mPhi]) / 100;
                        int meanDev = NAV_StringToInt(cmd_type[(int)NAV350_RCV_INDEX.nPosGet.mMeanDev]);

                        bool accept = true;

                        // 0) 항상 무시할 고정 쓰레기 패턴 차단
                        if (candX == 1900 && candY == 0 && candT == 0)
                        {
                            accept = false;
                        }

                        // 1) navMode == "1"만 수용
                        if (accept && navMode != "1")
                        {
                            accept = false;
                        }

                        // 2) 품질 기반 차단: MeanDev > 200
                        if (accept && meanDev > 200)
                        {
                            accept = false;
                        }

                        // 3) 동작 일관성 검사: 비현실적인 점프 차단
                        if (accept && hasLastGood)
                        {
                            long deltaPos = Math.Abs(candX - lastGoodX) + Math.Abs(candY - lastGoodY);
                            int range = (candT > 360 || lastGoodT > 360) ? 3600 : 360;
                            int angleThreshold = (range == 3600) ? 900 : 9;
                            int dRaw = Math.Abs(candT - (int)lastGoodT);
                            int deltaT = Math.Min(dRaw, range - dRaw);

                            if (deltaPos > 2000 || deltaT > angleThreshold)
                            {
                                accept = false;
                            }
                        }

                        // 유효 수신/처리 상태 업데이트
                        gNAVstate = (byte)NAV350_STATE.NAV_STATE_GET_POS;
                        gNAVCommand = (byte)NAV350_TRANSIT_CMD.CMD_POSITION;
                        Globals.system_error = Alarms.ALARM_NONE;
                        gTranscheck = 0;

                        // 최종 수용 시에만 전역 Pose 업데이트
                        if (accept && positionNavCount > 10)
                        //if (accept && positionNavCount > 10 && navMode == "1")
                        {
                            Globals.nav_x = candX;
                            Globals.nav_y = candY;
                            Globals.nav_phi = candT;
                            Globals.nav_dev = meanDev;

                            lastGoodX = candX;
                            lastGoodY = candY;
                            lastGoodT = candT;
                            hasLastGood = true;
                        }
                    }
                    positionNavCount++;
                    if (positionNavCount > 65533)
                        positionNavCount = 9;
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"NAV_RcvCMD_Position error: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        private static bool IsAllMultiplesOf10(int x, int y, int t)
        {
            return (x % 10 == 0) && (y % 10 == 0) && (t % 10 == 0);
        }

        static bool NAV_CopyRcvBuffer(out string[] cmd, string rcvData, int length)
        {
            cmd = null;
            string rcvDataTemp = string.Empty;

            try
            {
                // 기본 검증
                if (string.IsNullOrEmpty(rcvData) || length <= 0)
                {
                    return false;
                }

                if (length > rcvData.Length)
                {
                    length = rcvData.Length;
                }

                if (rcvData[0] == '\u0002')
                {
                    if (rcvData[length - 1] == '\u0003')
                    {
                        rcvDataTemp = string.Join("", rcvData.Split('\u0002', '\u0003'));
                        cmd = rcvDataTemp.Split(' ');

                        // 최대 배열 크기 제한
                        const int MAX_CMD_ARRAY_SIZE = 256;
                        if (cmd.Length > MAX_CMD_ARRAY_SIZE)
                        {
                            Tools.Log($"Command array too large: {cmd.Length}", Tools.ELogType.SystemLog);
                            cmd = null;
                            return false;
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"CopyRcvBuffer error: {ex.Message}", Tools.ELogType.SystemLog);
                cmd = null;
                return false;
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
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    int ReceiveDataSize = 0;
                    string ReceiveData = string.Empty;
                    string[] cmd = null;
                    gNavRcvErrorCheck = false;

                    lock (_socketLock)
                    {
                        if (socketSend == null || !socketSend.Connected)
                        {
                            _cancellationToken.WaitHandle.WaitOne(100);
                            continue;
                        }

                        ReceiveDataSize = socketSend.Available;
                        if (ReceiveDataSize > 0)
                        {
                            // 설정된 버퍼 크기 제한 사용
                            if (ReceiveDataSize > _maxReceiveBufferSize)
                            {
                                Tools.Log($"Receive buffer too large: {ReceiveDataSize}, limiting to {_maxReceiveBufferSize}", Tools.ELogType.SystemLog);
                                ReceiveDataSize = _maxReceiveBufferSize;
                            }

                            byte[] buffer = new byte[ReceiveDataSize];
                            socketSend.Receive(buffer);
                            ReceiveData = Encoding.UTF8.GetString(buffer, 0, ReceiveDataSize);
                        }
                    }

                    if (ReceiveDataSize > 0)
                    {
                        bool CopyError = NAV_CopyRcvBuffer(out cmd, ReceiveData, ReceiveDataSize);
                        if (!CopyError || cmd == null || cmd.Length == 0)
                        {
                            gNavRcvErrorCheck = true;
                            Tools.Log("Invalid command buffer received", Tools.ELogType.SystemLog);
                            continue;
                        }

                        int cmd_length = cmd.Length;

                        // 기본 유효성 검증
                        if (cmd[0].Equals("sFA"))
                        {
                            Globals.system_error = Alarms.ALARM_NAV350_COMM_CMD_ERROR;
                            NAV_ClearRcvBuffer(ref cmd, ref ReceiveData, ref ReceiveDataSize);
                            gNavRcvErrorCheck = true;
                            continue;
                        }

                        if (cmd_length < 2)
                        {
                            Globals.system_error = Alarms.ALARM_NAV350_COMM_INDEX_ERROR;
                            gNavRcvErrorCheck = true;
                            continue;
                        }

                        if (gNavRcvErrorCheck == false)
                        {
                            // pub_Cmd 인덱스 검증
                            if (cmd_length <= NAV350_RCV_INDEX.pub_Cmd)
                            {
                                Tools.Log($"Command array too short: {cmd_length}", Tools.ELogType.SystemLog);
                                continue;
                            }

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
                    }
                    else
                    {
                        // 데이터가 없을 때는 짧게 대기
                        _cancellationToken.WaitHandle.WaitOne(10);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Tools.Log("NAV RcvCheckThread cancelled gracefully", Tools.ELogType.SystemLog);
            }
            catch (SocketException sex)
            {
                if (!_cancellationToken.IsCancellationRequested)
                {
                    Tools.Log($"Socket receive error: {sex.SocketErrorCode}", Tools.ELogType.SystemLog);
                    _cancellationToken.WaitHandle.WaitOne(100);
                }
            }
            catch (Exception ex)
            {
                if (!_cancellationToken.IsCancellationRequested)
                {
                    Tools.Log($"RcvCheckThread error: {ex.Message}", Tools.ELogType.SystemLog);
                    _cancellationToken.WaitHandle.WaitOne(100);
                }
            }
            finally
            {
                Tools.Log("NAV RcvCheckThread exiting", Tools.ELogType.SystemLog);
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
            hasLastGood = false;
            lastGoodX = lastGoodY = lastGoodT = 0;
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

        // IDisposable 구현
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Tools.Log("NAVSensor disposing...", Tools.ELogType.SystemLog);

                // 스레드 종료 요청
                try
                {
                    _cancellationTokenSource?.Cancel();
                }
                catch (Exception ex)
                {
                    Tools.Log($"Error cancelling threads: {ex.Message}", Tools.ELogType.SystemLog);
                }

                // 메인 스레드 종료 대기
                try
                {
                    if (mainProcess != null && mainProcess.IsAlive)
                    {
                        if (!mainProcess.Join(TimeSpan.FromSeconds(5)))
                        {
                            Tools.Log("Main thread did not exit gracefully within timeout", Tools.ELogType.SystemLog);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Tools.Log($"Error waiting for main thread: {ex.Message}", Tools.ELogType.SystemLog);
                }

                // 작업 스레드 종료 대기
                try
                {
                    if (nav350TransThread != null && nav350TransThread.IsAlive)
                    {
                        if (!nav350TransThread.Join(TimeSpan.FromSeconds(2)))
                        {
                            Tools.Log("Trans thread did not exit gracefully within timeout", Tools.ELogType.SystemLog);
                        }
                    }

                    if (nav350RcvThread != null && nav350RcvThread.IsAlive)
                    {
                        if (!nav350RcvThread.Join(TimeSpan.FromSeconds(2)))
                        {
                            Tools.Log("Rcv thread did not exit gracefully within timeout", Tools.ELogType.SystemLog);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Tools.Log($"Error waiting for worker threads: {ex.Message}", Tools.ELogType.SystemLog);
                }

                // 소켓 정리
                lock (_socketLock)
                {
                    try
                    {
                        if (socketSend != null)
                        {
                            if (socketSend.Connected)
                            {
                                socketSend.Shutdown(SocketShutdown.Both);
                            }
                            socketSend.Close();
                            socketSend.Dispose();
                            socketSend = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Tools.Log($"Error disposing socket: {ex.Message}", Tools.ELogType.SystemLog);
                    }
                }

                // CancellationTokenSource 정리
                try
                {
                    _cancellationTokenSource?.Dispose();
                }
                catch (Exception ex)
                {
                    Tools.Log($"Error disposing CancellationTokenSource: {ex.Message}", Tools.ELogType.SystemLog);
                }

                Tools.Log("NAVSensor disposed successfully", Tools.ELogType.SystemLog);
            }

            _disposed = true;
        }

        // 소멸자
        ~NAVSensor()
        {
            Dispose(false);
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