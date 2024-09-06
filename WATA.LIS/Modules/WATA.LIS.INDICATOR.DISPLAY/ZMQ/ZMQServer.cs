
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.VISON;
using WATA.LIS.Core.Model.VISION;

namespace WATA.LIS.ZMQ
{
    class ZMQServer
    {

        private readonly IEventAggregator _eventAggregator;

        private Thread ZmqThread;


        public ZMQServer(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

        }


        public  void Init()
        {
            Tools.Log($" Init ", Tools.ELogType.DisplayLog);
            ZmqThread = new Thread(new ThreadStart(ZMQReceiveInit));
            ZmqThread.Start();

        }

        

        private void ZMQReceiveInit()
        {
            Tools.Log($"InitZMQ", Tools.ELogType.VisionLog);
            using (var subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 10000;
                subSocket.Connect("tcp://192.168.1.86:8051");
                subSocket.Subscribe("");
                //subSocket.Subscribe("vision_forklift");
                //subSocket.Subscribe("WATA");

                while (true)
                {
                    try
                    {
                        string RecieveStr = subSocket.ReceiveFrameString();
                        Tools.Log($"receive {RecieveStr}", Tools.ELogType.DisplayLog);
                        SysError.RemoveErrorCode(SysError.VisionConnError);
                    }
                    catch
                    {
                        Tools.Log($"Exception!!!", Tools.ELogType.DisplayLog);
                        SysError.AddErrorCode(SysError.VisionConnError);
                    }
                    Thread.Sleep(50);
                }
            }
        }
    }
}
