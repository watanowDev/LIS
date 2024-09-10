
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
using Windows.Storage.Streams;

namespace WATA.LIS.TCPSocket
{
    class TcpClientSimple
    {


        private readonly IEventAggregator _eventAggregator;
        static private string _ip;
        static private int _port;
        static TcpClient _client;
        public TcpClientSimple(IEventAggregator eventAggregator, string ip, int port)
        {
            this._eventAggregator = eventAggregator;

            _eventAggregator.GetEvent<DPSSendEvent>().Subscribe(onSendData, ThreadOption.BackgroundThread, true);
            _ip = ip;
            _port = port;
        }

        public async Task InitAsync()
        {

            ConnectToServer();
        }




        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private CancellationTokenSource cancellationTokenSource;




        private async Task ConnectToServer()
        {

            Tools.Log($"Try to Connect IP : {_ip} PORT : {_port}", Tools.ELogType.DPSLog);

            try
            {
                if (tcpClient != null)
                {

                    return;
                }

                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(_ip, _port);

                if (tcpClient.Connected)
                {
                    Tools.Log($"Connect Success", Tools.ELogType.DPSLog);
                    SysAlarm.RemoveErrorCodes(SysAlarm.DPSConnErr);

                    networkStream = tcpClient.GetStream();
                    cancellationTokenSource = new CancellationTokenSource();
                    _ = Task.Run(() => ReceiveData(cancellationTokenSource.Token));
                }
                else
                {
                    Tools.Log($"Disconnect Server", Tools.ELogType.DPSLog);
                    SysAlarm.AddErrorCodes(SysAlarm.DPSConnErr);
                }

            }
            catch (Exception ex)
            {
                Tools.Log($"Exception {ex.Message}", Tools.ELogType.DPSLog);
                SysAlarm.AddErrorCodes(SysAlarm.DPSConnErr);
            }
        }



        private void onSendData(byte[] data)
        {

            OnSendDataAsync(data);

        }


        private async Task OnSendDataAsync(byte[] data)
        {
            string bytelog = Util.DebugBytestoString(data);
            Tools.Log($"Send packet {bytelog}", Tools.ELogType.DPSLog);


            try
            {
                if (tcpClient != null && tcpClient.Connected)
                {
                    //      byte[] data = Encoding.UTF8.GetBytes(msg);
                    await networkStream.WriteAsync(data, 0, data.Length);
                    SysAlarm.RemoveErrorCodes(SysAlarm.DPSConnErr);
                    SysAlarm.RemoveErrorCodes(SysAlarm.DPSRcvErr);
                }
                else
                {
                    Tools.Log($"DisConnect: {_ip} PORT : {_port}", Tools.ELogType.DPSLog);
                    SysAlarm.AddErrorCodes(SysAlarm.DPSConnErr);
                }


            }
            catch (Exception ex)
            {
                Tools.Log($"ReceiveData Exception {ex.Message}", Tools.ELogType.DPSLog);
                SysAlarm.AddErrorCodes(SysAlarm.DPSRcvErr);
            }
        }



        private async Task ReceiveData(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead > 0)
                    {
                        SysAlarm.RemoveErrorCodes(SysAlarm.DPSConnErr, SysAlarm.DPSRcvErr);
                    }

                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {

                Tools.Log($"ReceiveData Exception {ex.Message}", Tools.ELogType.DPSLog);
                SysAlarm.AddErrorCodes(SysAlarm.DPSRcvErr);

                tcpClient.Close();
                tcpClient = null;
            }
        }















        public void onSendMessage(byte[] buffer)
        {
            string bytelog = Util.DebugBytestoString(buffer);
            Tools.Log($"Send packet {bytelog}", Tools.ELogType.DPSLog);

            try
            {
                if (_client != null)
                {

                    NetworkStream stream = _client.GetStream();
                    stream.Write(buffer, 0, buffer.Length);

                }
                else
                {
                    Tools.Log($"Disconnect client", Tools.ELogType.DPSLog);

                }
            }
            catch
            {
                _client.Close();
                Tools.Log($"Send Exception", Tools.ELogType.DPSLog);
            }
        }

        public static async Task StartListenerAsync()
        {
            try
            {
                Tools.Log($"Try to Connect IP : {_ip} PORT : {_port}", Tools.ELogType.DPSLog);

                if (!IPAddress.TryParse(_ip, out IPAddress ipAddress))
                {
                    Tools.Log("Invalid IP address", Tools.ELogType.DPSLog);
                    return;
                }

                if (_port < 0 || _port > 65535)
                {
                    Tools.Log("Invalid port number", Tools.ELogType.DPSLog);
                    return;
                }
                TcpListener listener = new TcpListener(ipAddress, _port);

                listener.Start();
                while (true)
                {
                    _client = await listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(_client);
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Exception: {ex.Message}", Tools.ELogType.DPSLog);
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                // 클라이언트로부터 데이터 수신
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                // Handle received data
            }
            catch (Exception ex)
            {
                client.Close();
                Tools.Log($"Exception: {ex.Message}", Tools.ELogType.DPSLog);
            }
        }


    }
}
