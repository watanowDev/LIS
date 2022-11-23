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
        DispatcherTimer rftag_valid_timer;




        private readonly IEventAggregator _eventAggregator;
        public RFID_SENSOR(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            _eventAggregator.GetEvent<RFIDSensorEvent>().Subscribe(OnRFIDSensorData, ThreadOption.BackgroundThread, true);
        }

        public void OnRFIDSensorData(RFIDSensorModel obj)
        {
          if(obj.EPC_Data =="NA")
          {
                m_cnt = 0;
                m_before_epc = "";

            }

        }

        public void Init()
        {
            RecvThread = new Thread(new ThreadStart(ZMQReceiveInit));
            RecvThread.Start();

            DispatcherTimer WPS_Process_chk_Timer = new DispatcherTimer();
            WPS_Process_chk_Timer.Interval = new TimeSpan(0, 0, 0, 0, 10000);
            WPS_Process_chk_Timer.Tick += new EventHandler(AliveTimerEvent);
            WPS_Process_chk_Timer.Start();


            rftag_valid_timer = new DispatcherTimer();
            rftag_valid_timer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            rftag_valid_timer.Tick += new EventHandler(rfTagVaildTimerEvent);
            //rftag_valid_timer.Start();


            ExecuteWPS();
        }


        private void rfTagVaildTimerEvent(object sender, EventArgs e)
        {
            //ExecuteWPS();
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

        private string m_before_epc = "";
        private int m_cnt = 0;

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

                        if (m_before_epc == RecieveStr)
                        {
                            if (m_cnt >= 10)
                            {

                                _eventAggregator.GetEvent<RFIDSensorEvent>().Publish(rfidmodel);
                                m_cnt = 0;
                                m_before_epc = "";


                                Tools.Log($"##Enable RFTAG {RecieveStr} ", Tools.ELogType.BackEndLog);
                            }
                            m_cnt++;
                        }
                        else
                        {
                            m_cnt = 0;
                            m_before_epc = "";
                            Tools.Log($"##Clear Rest RFTAG {RecieveStr} ", Tools.ELogType.BackEndLog);
                        }
                        

                        m_before_epc = RecieveStr;
                    }
                    Thread.Sleep(50);
                }
            }
        }
    }
}
