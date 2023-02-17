using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.VISION;

namespace WATA.LIS.VISION.Camera.Camera
{
    public class AstraCamera
    {
        Thread RecvThread;

        int statuscheckCount = 0;


        private readonly IEventAggregator _eventAggregator;
        private readonly IVisionModel _visonModel;
        VisionConfigModel visionConfig;
        public AstraCamera(IEventAggregator eventAggregator, IVisionModel visionModel)
        {
            _eventAggregator = eventAggregator;
            _visonModel = visionModel;
            visionConfig = (VisionConfigModel)_visonModel;
        }

        public void Init()
        {
            RecvThread = new Thread(new ThreadStart(ZMQReceiveInit));
            RecvThread.Start();

            DispatcherTimer Process_chk_Timer = new DispatcherTimer();
            Process_chk_Timer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            Process_chk_Timer.Tick += new EventHandler(AliveTimerEvent);
            Process_chk_Timer.Start();

            StartProcess();
        }

        private void AliveTimerEvent(object sender, EventArgs e)
        {
            //StartProcess();


            if(statuscheckCount  >=15)
            {
                RecoveryProcess();

            }

            statuscheckCount++;
        }

        private void RecoveryProcess()
        {
            statuscheckCount = 0;

            StopProcess();
            Thread.Sleep(5000);
            StartProcess();

            Tools.Log($"Recovery Process ", Tools.ELogType.VisionLog);
        }

        private void StopProcess()
        {

            Process[] processList_vision = Process.GetProcessesByName("vision_forklift");
            for (int i = processList_vision.Length - 1; i >= 0; i--)
            {
                // processList[i].CloseMainWindow();
                processList_vision[i].Kill();
                processList_vision[i].Close();
            }

            Tools.Log($"Stop Process ", Tools.ELogType.VisionLog);
        }


        private void StartProcess()
        {
            Process[] processes = Process.GetProcessesByName("vision_forklift.exe");
            if (processes.Length == 0)
            {
                
                string dir = System.IO.Directory.GetCurrentDirectory() + "\\VISION\\";
                try
                {
                    if (true)
                    {
                        ProcessStartInfo procInfo = new ProcessStartInfo();
                        procInfo.UseShellExecute = true;
                        procInfo.FileName = "vision_forklift.exe";
                        procInfo.WorkingDirectory = dir;
                        procInfo.ArgumentList.Add("vision");
                        procInfo.ArgumentList.Add(visionConfig.CameraHeight);
                        procInfo.ArgumentList.Add(visionConfig.QRValue);
                        Process.Start(procInfo);
                        Tools.Log($"Vision Parameter QRValue {visionConfig.QRValue} CameraHeight {visionConfig.CameraHeight} ", Tools.ELogType.VisionLog);
                    }
                    else
                    {
                        ProcessStartInfo procInfoOld = new ProcessStartInfo();
                        procInfoOld.UseShellExecute = true;
                        procInfoOld.FileName = "vision_forklift.exe";
                        procInfoOld.WorkingDirectory = dir;
                        procInfoOld.ArgumentList.Add("vision");
                        procInfoOld.ArgumentList.Add("0.3");
                        procInfoOld.ArgumentList.Add("45");
                        procInfoOld.ArgumentList.Add("0");
                        Process.Start(procInfoOld);
                    }
                    
            
                  
                }
                catch (Exception ex)
                {
                   // MessageBox.Show(ex.Message.ToString());
                }
            }
            Tools.Log($"Start Process ", Tools.ELogType.VisionLog);
        }

        private void ZMQReceiveInit()
        {
            Tools.Log($"InitZMQ", Tools.ELogType.VisionLog);
            using (var subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 10000;
                subSocket.Connect("tcp://localhost:5555");
                subSocket.Subscribe("vision");
                subSocket.Subscribe("vision_forklift");
                subSocket.Subscribe("WATA");

                while (true)
                {
                    try
                    {
                        string RecieveStr = subSocket.ReceiveFrameString();

                        if (RecieveStr.Contains("vision"))
                        {
                            Tools.Log("Topic : " + RecieveStr, Tools.ELogType.VisionLog);

                        }
                        else
                        {
                            Tools.Log("Body : " + RecieveStr, Tools.ELogType.VisionLog);

                            VISON_Model visionModel = new VISON_Model();


                            JObject jObject = JObject.Parse(RecieveStr);
                            if (jObject.ContainsKey("area") == true)
                            {
                                visionModel.area = (float)jObject["area"];
                            }
                        
                            if (jObject.ContainsKey("width") == true)
                            {
                                visionModel.width = (float)jObject["width"];
                            }

                            if (jObject.ContainsKey("height") == true)
                            {
                                visionModel.height = (int)jObject["height"];
                            }

                            if (jObject.ContainsKey("qr") == true)
                            {
                                visionModel.qr = jObject["qr"].ToString();
                            }

                            if (jObject.ContainsKey("status") == true)
                            {
                                visionModel.status = jObject["status"].ToString();

                                if(visionModel.status == "dead")
                                {
                                    //StopProcess();
                                    Tools.Log($"Receive dead", Tools.ELogType.VisionLog);
                                }
                                else
                                {

                                    statuscheckCount = 0;
                                }

                            }

                            if (jObject.ContainsKey("matrix") == true)
                            {
                                visionModel.matrix = jObject["matrix"].ToObject<byte[]>();

                            }

                            if (visionModel.status == "drop" || visionModel.status == "pickup")
                            {
                                _eventAggregator.GetEvent<VISION_Event>().Publish(visionModel);
                                Tools.Log("### Send Vision Event Action ###", Tools.ELogType.VisionLog);
                            }


                            Tools.Log($"ParseModel  area : {visionModel.area} width : {visionModel.width} height : {visionModel.height} qr : {visionModel.qr} status : {visionModel.status} ", Tools.ELogType.VisionLog);
                        }
                    }
                    catch
                    {
                        Tools.Log($"Exception!!!", Tools.ELogType.VisionLog);
                    }
                    Thread.Sleep(50);

                  
                }
            }
        }
    }
}
