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
    public class WPSControl
    {
        Thread TableRFIDRecvThread;
        Thread LocationRFIDRecvThread;

        private readonly IEventAggregator _eventAggregator;
        public WPSControl(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

        }

       
        public void Init()
        {
            TableRFIDRecvThread = new Thread(new ThreadStart(TableRFID_ZMQReceiveInit));
            TableRFIDRecvThread.Start();

            LocationRFIDRecvThread = new Thread(new ThreadStart(LocationRFID_ZMQReceiveInit));
            LocationRFIDRecvThread.Start();


            
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


        private void TableRFID_ZMQReceiveInit()
        {
            Tools.Log($"InitZMQ", Tools.ELogType.RFIDLog);
            using (var subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 10000;
                subSocket.Connect("tcp://localhost:8051");
                subSocket.Subscribe("TABLE");

                while (true)
                {
                    byte[] messageReceived = subSocket.ReceiveFrameBytes();

                    string RecieveStr = Util.BytesToString(messageReceived);
                    if(RecieveStr == "TABLE")
                    {
                        //Tools.Log("Topic : "+ Util.BytesToString(messageReceived), Tools.ELogType.RFIDLog);

                    }
                    else
                    {
                        Tools.Log("Body : " + Util.BytesToString(messageReceived), Tools.ELogType.RFIDLog);
                        RackRFIDEventModel rfidmodel = new RackRFIDEventModel();
                        
                        if (RecieveStr.Length == 24)
                        {
                            string stx = RecieveStr.Substring(0, 2);
                            string etx = RecieveStr.Substring(22, 2);

                            if(stx == "DA" && etx == "ED")
                            {
                                rfidmodel.EPC = RecieveStr;
                                _eventAggregator.GetEvent<RackProcess_Event>().Publish(rfidmodel);
                            }
                            else
                            {

                               
                            }
                        }

                    }
                    Thread.Sleep(10);
                }
            }
        }



        private void LocationRFID_ZMQReceiveInit()
        {
            Tools.Log($"InitZMQ", Tools.ELogType.RFIDLog);
            using (var subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 10000;
                subSocket.Connect("tcp://localhost:8052");
                subSocket.Subscribe("LOCATION");

                while (true)
                {
                    byte[] messageReceived = subSocket.ReceiveFrameBytes();



                    string RecieveStr = Util.BytesToString(messageReceived);
                    
                    if (RecieveStr == "LOCATION")
                    {
                           

                    }
                    else
                    {
                        if (RecieveStr.Length == 24)
                        {

                            string stx = RecieveStr.Substring(0, 2);
                            string etx = RecieveStr.Substring(22, 2);


                            if (stx == "DA" && etx == "ED")
                            {
                                LocationRFIDEventModel location = new LocationRFIDEventModel();
                                location.EPC = RecieveStr;
                                _eventAggregator.GetEvent<LocationProcess_Event>().Publish(location);

                                //Tools.Log("Topic : " + Util.BytesToString(messageReceived), Tools.ELogType.RFIDLog);
                            }



                            Tools.Log("Body : " + Util.BytesToString(messageReceived), Tools.ELogType.RFIDLog);
                        }
                    }
                    Thread.Sleep(10);
                }
            }
        }
    }
}
