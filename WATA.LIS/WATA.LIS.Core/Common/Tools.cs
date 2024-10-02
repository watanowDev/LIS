using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.IO;
using Newtonsoft.Json;
using log4net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using log4net.Config;
using Prism.Services.Dialogs;

namespace WATA.LIS.Core.Common
{
    public class Tools
    {
        static BrushConverter converter = new System.Windows.Media.BrushConverter();
        static ILog SystemLog;
        static ILog RFIDLog;
        static ILog DistanceLog;
        static ILog VisionLog;
        static ILog BackEndLog;
        static ILog BackEndCurrentELog;
        static ILog ActionLog;
        static ILog WeightLog;
        static ILog DisplayLog;
        static ILog DPSLog;
        static ILog NAVLog;
        static ILog VisionCamLog;

        static public LogInfo logInfo { get; set; } = new LogInfo();

        static Tools()
        {
            var ITEM = XmlConfigurator.Configure(new System.IO.FileInfo(@".\\Log4Net_WATA.xml"));
            SystemLog = LogManager.GetLogger("SystemLogEx");
            RFIDLog = LogManager.GetLogger("RFIDLogEx");
            DistanceLog = LogManager.GetLogger("DistanceLogEx");
            VisionLog = LogManager.GetLogger("VisionLogEx");
            BackEndLog = LogManager.GetLogger("BackEndLogEx");
            BackEndCurrentELog = LogManager.GetLogger("BackEndCurrentLogEx");
            ActionLog = LogManager.GetLogger("ActionLogEx");
            WeightLog = LogManager.GetLogger("WeightLogEx");
            DisplayLog = LogManager.GetLogger("DisplayLogEx");
            DPSLog = LogManager.GetLogger("DPSLogEx");
            NAVLog = LogManager.GetLogger("NAVLogEx");
            VisionCamLog = LogManager.GetLogger("QRCameraLogEx");
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyName name = assembly.GetName();
        }

        public static byte[] UShortToByte(ushort value)
        {
            return BitConverter.GetBytes(value);
        }


        static public void Log(string message, ELogType logType,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null)
        {
            try
            {
                string msg = $"{caller}({lineNumber}) > {message}";
                if (logType != ELogType.None)
                {
                    switch (logType)
                    {
                        case ELogType.DPSLog:
                            DPSLog.Info(msg);//Point
                            if (logInfo.ListDPSLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListDPSLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListDPSLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListDPSLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListDPSLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;



                        case ELogType.DisplayLog:
                            DisplayLog.Info(msg);//Point
                            if (logInfo.ListDisplayLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListDisplayLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListDisplayLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListDisplayLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListDisplayLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;


                        case ELogType.WeightLog:
                            WeightLog.Info(msg);//Point
                            if (logInfo.ListWeightLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListWeightLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListWeightLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListWeightLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListWeightLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;


                        case ELogType.ActionLog:
                            ActionLog.Info(msg);//Point
                            if (logInfo.ListActionLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListActionLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListActionLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListActionLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListActionLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;

                        case ELogType.BackEndCurrentLog:
                            BackEndCurrentELog.Info(msg);//Point
                            if (logInfo.ListBackEndCurrentLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListBackEndCurrentLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListBackEndCurrentLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListBackEndCurrentLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListBackEndCurrentLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        case ELogType.SystemLog:
                            SystemLog.Info(msg);//Point
                            if (logInfo.ListSystemLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListSystemLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListSystemLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListSystemLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListSystemLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        case ELogType.RFIDLog:
                            RFIDLog.Info(msg);//Point
                            if (logInfo.ListRFIDLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListRFIDLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListRFIDLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListRFIDLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListRFIDLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        case ELogType.DistanceLog:
                            DistanceLog.Info(msg);//Point
                            if (logInfo.ListDistanceLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListDistanceLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListDistanceLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListDistanceLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListDistanceLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        case ELogType.VisionLog:
                            VisionLog.Info(msg);//Point
                            if (logInfo.ListVisionLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListVisionLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListVisionLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListVisionLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListVisionLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        case ELogType.NAVLog:
                            NAVLog.Info(msg);//Point
                            if (logInfo.ListNAVLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListNAVLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListNAVLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListNAVLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListNAVLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        case ELogType.BackEndLog:
                            BackEndLog.Info(msg);//Point
                            if (logInfo.ListBackEndLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListBackEndLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListBackEndLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListBackEndLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListBackEndLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        case ELogType.VisionCamLog:
                            VisionCamLog.Info(msg);//Point
                            if (logInfo.ListVisionCamLog.Count > 300)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListVisionCamLog.Clear(); }
                                , DispatcherPriority.Normal);
                            }
                            if (logInfo.ListVisionCamLog.Count > 0)//Point
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                {
                                    logInfo.ListVisionCamLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message));//Point
                                }
                                , DispatcherPriority.Normal);
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(delegate
                                { logInfo.ListVisionCamLog.Add(new Log(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), caller, message)); }//Point
                                , DispatcherPriority.Normal);
                            }
                            break;
                        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"{ex.Message}", Tools.ELogType.SystemLog);
            }
        }


        static public void SaveJsons<T>(T json, string filename)
        {
            string jsonData = JsonConvert.SerializeObject(json);

            string Path = $@"{AppDomain.CurrentDomain.BaseDirectory}{filename}";

            File.WriteAllText(Path, jsonData);
        }

        static public T LoadJson<T>(string FileName)
        {
            T jsonData = default(T);
            try
            {
                string Path = $@"{AppDomain.CurrentDomain.BaseDirectory}{FileName}";

                if (File.Exists(Path))
                {
                    jsonData = JsonConvert.DeserializeObject<T>(File.ReadAllText(Path));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return jsonData;
        }


        static public Brush Brush(EColor color)
        {
            Brush Brush;
            switch (color)
            {
                case EColor.GREEN:
                    Brush = (Brush)converter.ConvertFromString("#FF68F33E");
                    break;
                case EColor.RED:
                    Brush = (Brush)converter.ConvertFromString("#FFFF0505");
                    break;
                case EColor.GRAY:
                    Brush = (Brush)converter.ConvertFromString("#FFD8D8D8");
                    break;
                case EColor.BTN_GRAY:
                    Brush = (Brush)converter.ConvertFromString("#FFF7F7F7");
                    break;
                case EColor.WHITE:
                    Brush = (Brush)converter.ConvertFromString("#FFFFFFFF");
                    break;
                case EColor.BLACK:
                    Brush = (Brush)converter.ConvertFromString("#FF000000");
                    break;
                case EColor.DARK_GRAY:
                    Brush = (Brush)converter.ConvertFromString("#FF7E7E7E");
                    break;
                case EColor.DARK_BLUE:
                    Brush = (Brush)converter.ConvertFromString("#FF0051a5");
                    break;
                case EColor.LIGHT_BLUE:
                    Brush = (Brush)converter.ConvertFromString("#FF42a2e7");
                    break;
                case EColor.BACKGROUND_WHITE:
                    Brush = (Brush)converter.ConvertFromString("#FFf7f7f7");
                    break;
                case EColor.BTN_MOUSE_OVER:
                    Brush = (Brush)converter.ConvertFromString("#FFC4E1FF");
                    break;
                case EColor.BTN_DISABLE_FOREGROUND:
                    Brush = (Brush)converter.ConvertFromString("#ffb3b3b3");
                    break;
                case EColor.BTN_DISABLE_BACKGROUND:
                    Brush = (Brush)converter.ConvertFromString("#ffE7E7E7");
                    break;
                case EColor.Door_Open:
                    Brush = (Brush)converter.ConvertFromString("#FFfafafa");
                    break;
                case EColor.Door_Dock:
                    Brush = (Brush)converter.ConvertFromString("#FFa2d5f2");
                    break;
                case EColor.Door_Lock:
                    Brush = (Brush)converter.ConvertFromString("#FF07689f");
                    break;

                case EColor.TowerLamp_Red_Off:
                    Brush = (Brush)converter.ConvertFromString("#FF6f0000");
                    break;
                case EColor.TowerLamp_Red_On:
                    Brush = (Brush)converter.ConvertFromString("#FFff0000");
                    break;
                case EColor.TowerLamp_Yellow_Off:
                    Brush = (Brush)converter.ConvertFromString("#FF816600");
                    break;
                case EColor.TowerLamp_Yellow_On:
                    Brush = (Brush)converter.ConvertFromString("#FFfff612");
                    break;
                case EColor.TowerLamp_Green_Off:
                    Brush = (Brush)converter.ConvertFromString("#FF004b00");
                    break;
                case EColor.TowerLamp_Green_On:
                    Brush = (Brush)converter.ConvertFromString("#FF2fed28");
                    break;


                default:
                    Brush = (Brush)converter.ConvertFromString("#FFf7f7f7");
                    break;
            }
            return Brush;
        }

        static public string strBrush(EColor color)
        {
            string Brush;
            switch (color)
            {
                case EColor.GREEN:
                    Brush = "#FF68F33E";
                    break;
                case EColor.RED:
                    Brush = "#FFFF0505";
                    break;
                case EColor.GRAY:
                    Brush = "#FFD8D8D8";
                    break;
                case EColor.BTN_GRAY:
                    Brush = "#FFF7F7F7";
                    break;
                case EColor.WHITE:
                    Brush = "#FFFFFFFF";
                    break;
                case EColor.BLACK:
                    Brush = "#FF000000";
                    break;
                case EColor.DARK_GRAY:
                    Brush = "#FF7E7E7E";
                    break;
                case EColor.DARK_BLUE:
                    Brush = "#FF0051a5";
                    break;
                case EColor.LIGHT_BLUE:
                    Brush = "#FF42a2e7";
                    break;
                case EColor.BACKGROUND_WHITE:
                    Brush = "#FFf7f7f7";
                    break;
                case EColor.BTN_MOUSE_OVER:
                    Brush = "#FFC4E1FF";
                    break;
                case EColor.BTN_DISABLE_FOREGROUND:
                    Brush = "#FFB3B3B3";
                    break;
                case EColor.BTN_DISABLE_BACKGROUND:
                    Brush = "#FFE7E7E7";
                    break;
                case EColor.Door_Open:
                    Brush = "#FFFAFAFA";
                    break;
                case EColor.Door_Dock:
                    Brush = "#FFA2D5F2";
                    break;
                case EColor.Door_Lock:
                    Brush = "#FF07689F";
                    break;

                case EColor.TowerLamp_Red_Off:
                    Brush = "#FF6f0000";
                    break;
                case EColor.TowerLamp_Red_On:
                    Brush = "#FFFF0000";
                    break;
                case EColor.TowerLamp_Yellow_Off:
                    Brush = "#FF816600";
                    break;
                case EColor.TowerLamp_Yellow_On:
                    Brush = "#FFFFF612";
                    break;
                case EColor.TowerLamp_Green_Off:
                    Brush = "#FF004B00";
                    break;
                case EColor.TowerLamp_Green_On:
                    Brush = "#FF2FED28";
                    break;

                case EColor.TowerLamp_Blue_Off:
                    Brush = "#FF9190FF";
                    break;
                case EColor.TowerLamp_Blue_On:
                    Brush = "#FF0000C9";
                    break;



                default:
                    Brush = "#FFF7F7F7";
                    break;
            }
            return Brush;
        }

        public enum EColor
        {
            GREEN,
            RED,
            GRAY,
            BTN_GRAY,
            WHITE,
            BLACK,
            DARK_GRAY,
            DARK_BLUE,
            LIGHT_BLUE,
            BACKGROUND_WHITE,
            BTN_MOUSE_OVER,
            BTN_DISABLE_FOREGROUND,
            BTN_DISABLE_BACKGROUND,
            Door_Open,
            Door_Dock,
            Door_Lock,

            TowerLamp_Red_Off,
            TowerLamp_Red_On,
            TowerLamp_Yellow_Off,
            TowerLamp_Yellow_On,
            TowerLamp_Green_Off,
            TowerLamp_Green_On,

            TowerLamp_Blue_Off,
            TowerLamp_Blue_On,
        }


        public enum ELogType
        {
            None,
            SystemLog,
            RFIDLog,
            DistanceLog,
            VisionLog,
            BackEndLog,
            BackEndCurrentLog,
            ActionLog,
            WeightLog,
            DisplayLog,
            DPSLog,
            NAVLog,
            VisionCamLog
        }

        public struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }
        [DllImport("kernel32.dll")]
        public extern static uint SetSystemTime(ref SYSTEMTIME lpSystemTime);

    }
}
