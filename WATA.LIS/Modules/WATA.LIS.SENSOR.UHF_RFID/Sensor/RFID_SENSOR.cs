using NetMQ;
using NetMQ.Sockets;
using Prism.Events;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;

namespace WATA.LIS.SENSOR.Sensor
{
    public class RFID_SENSOR
    {
        Thread RecvThread;

        public RFID_SENSOR(IEventAggregator eventAggregator)
        {


            RecvThread = new Thread(new ThreadStart(ZMQReceiveInit));
            RecvThread.Start();

        }

        private void ZMQReceiveInit()
        {
            Tools.Log($"InitZMQ", Tools.ELogType.RFIDLog);
            using (var subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 10000;
                subSocket.Connect("tcp://localhost:8051");
                subSocket.Subscribe("RFID");

                while (true)
                {
                    byte[] messageReceived = subSocket.ReceiveFrameBytes();
                    Tools.Log(Util.BytesToString(messageReceived), Tools.ELogType.RFIDLog);
                    Thread.Sleep(100);
                }
            }
        }
    }
}
