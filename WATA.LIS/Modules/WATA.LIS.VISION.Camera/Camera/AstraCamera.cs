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
using WATA.LIS.Core.Model.VISION;

namespace WATA.LIS.VISION.Camera.Camera
{
    public class AstraCamera
    {
        Thread RecvThread;

        private readonly IEventAggregator _eventAggregator;
        public AstraCamera(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public void Init()
        {
            RecvThread = new Thread(new ThreadStart(ZMQReceiveInit));
            RecvThread.Start();

            DispatcherTimer Process_chk_Timer = new DispatcherTimer();
            Process_chk_Timer.Interval = new TimeSpan(0, 0, 0, 0, 5000);
            Process_chk_Timer.Tick += new EventHandler(AliveTimerEvent);
            //Process_chk_Timer.Start();

            StartProcess();
        }

        private void AliveTimerEvent(object sender, EventArgs e)
        {
            StartProcess();
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
                        procInfo.ArgumentList.Add("1.15");
                        procInfo.ArgumentList.Add("1");
                        Process.Start(procInfo);
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
                        //Tools.Log($"Exception!!!", Tools.ELogType.VisionLog);
                    }
                    Thread.Sleep(50);
                }
            }
        }
    }
}
