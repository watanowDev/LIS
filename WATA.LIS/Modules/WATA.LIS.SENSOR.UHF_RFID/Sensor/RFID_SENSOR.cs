using NetMQ;
using NetMQ.Sockets;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Model.DistanceSensor;
using WATA.LIS.Core.Model.RFID;







namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class RFID_SENSOR
    {
        Thread RecvThread;


        
        private readonly IEventAggregator _eventAggregator;
        public RFID_SENSOR(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public void Init()
        {
            RecvThread = new Thread(new ThreadStart(ZMQReceiveInit));
            RecvThread.Start();

            DispatcherTimer WPS_Process_chk_Timer = new DispatcherTimer();
            WPS_Process_chk_Timer.Interval = new TimeSpan(0, 0, 0, 0, 10000);
            WPS_Process_chk_Timer.Tick += new EventHandler(AliveTimerEvent);
            WPS_Process_chk_Timer.Start();


            ExecuteWPS();
        }

        private void AliveTimerEvent(object sender, EventArgs e)
        {
            ExecuteWPS();
        }


        private void ExecuteWPS()
        {
            Process[] processes = Process.GetProcessesByName("WATA.LIS.WPS");
            if (processes.Length == 0)
            {

                string dir = System.IO.Directory.GetCurrentDirectory() + "\\WPS\\WATA.LIS.WPS.exe";
                Process.Start(dir);
            }
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

                    string RecieveStr = Util.BytesToString(messageReceived);
                    if(RecieveStr.Length == 4)
                    {
                        Tools.Log("Topic : "+ Util.BytesToString(messageReceived), Tools.ELogType.RFIDLog);

                    }
                    else
                    {
                        Tools.Log("Body : " + Util.BytesToString(messageReceived), Tools.ELogType.RFIDLog);
                        RFIDSensorModel rfidmodel = new RFIDSensorModel();
                        rfidmodel.EPC_Data = RecieveStr;
                        _eventAggregator.GetEvent<RFIDSensorEvent>().Publish(rfidmodel);
                    }
                    Thread.Sleep(50);
                }
            }
        }
    }
}
