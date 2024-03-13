
using Newtonsoft.Json.Linq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DPS;
using WATA.LIS.Core.Events.Indicator;
using Windows.Media.Protection.PlayReady;

namespace WATA.LIS.TCPSocket
{ 
    class TcpClientSimple
    {


        private readonly IEventAggregator _eventAggregator;
        static  private string _ip;
        static  private int _port;
        TcpClient client;
        public TcpClientSimple(IEventAggregator eventAggregator, string ip, int port)
        {
            this._eventAggregator = eventAggregator;
            _ip = ip;
            _port = port;
            _eventAggregator.GetEvent<DPSSendEvent>().Subscribe(onSendMessage, ThreadOption.BackgroundThread, true);
            StartListener();
        }

        public void onSendMessage(byte[] buffer)
        {
            NetworkStream stream = client.GetStream();
            // 클라이언트에게 응답을 송신
            string responseMessage = "Hello from server!";
            byte[] data = Encoding.UTF8.GetBytes(responseMessage);
            stream.Write(data, 0, data.Length);
            client.Close();
        }


        static void StartListener()
        {
            try
            {
                Tools.Log($"Try to Connect IP : {_ip} PORT : {_port}", Tools.ELogType.DPSLog);

                IPAddress ipAddress = IPAddress.Parse(_ip);
                int port = _port;

                TcpListener listener = new TcpListener(ipAddress, port);
                listener.Start();

                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
            }
        }

        static void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                // 클라이언트로부터 데이터 수신
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                Encoding.UTF8.GetString(buffer, 0, bytesRead);
                

            }
            catch (Exception ex)
            {
                client.Close();
            }
        }
    }
}
