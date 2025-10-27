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
using WATA.LIS.Core.Events.Indicator;
using Windows.Media.Protection.PlayReady;

namespace WATA.LIS.TCPSocket
{
    class TcpServerSimple
    {
        Socket _passiveReceiveSocket;
        Thread _passiveTCPReceive;
        DispatcherTimer AliveTimer = new DispatcherTimer();
        public delegate void MsgEvent(byte[] msg);
        public event MsgEvent msg;

        TcpClient _client;

        private readonly IEventAggregator _eventAggregator;

        // 로그 스로틀링 상태(1초 간격)
        private static DateTime _lastDisplayLogUtc = DateTime.MinValue;
        private static int _displayLogSuppressed = 0;

        public TcpServerSimple(IEventAggregator eventAggregator)
        {
            this._eventAggregator = eventAggregator;

            _eventAggregator.GetEvent<IndicatorSendEvent>().Subscribe(onSendMessageAsync, ThreadOption.BackgroundThread, true);

        }

        public async void onSendMessageAsync(string SendMessage)
        {
            if (_client == null)
            {
                return;

            }


            try
            {
                if (_client.Connected)
                {
                    NetworkStream stream = _client.GetStream();
                    byte[] response = Encoding.UTF8.GetBytes(SendMessage);
                    await stream.WriteAsync(response, 0, response.Length);

                    //Tools.Log($"{SendMessage}", Tools.ELogType.DisplayLog);

                }

            }
            catch
            {


            }



        }


        public async Task initAsync()
        {
            TcpListener server = null;

            try
            {
                // IP 주소와 포트 번호를 설정
                IPAddress ipAddress = IPAddress.Parse("0.0.0.0"); // 모든 IP 주소에 바인딩
                int port = 8051;

                // TcpListener 인스턴스 생성
                server = new TcpListener(ipAddress, port);

                Tools.Log($"TCP Server Listening on Port {port}", Tools.ELogType.SystemLog);

                // 클라이언트 연결 대기
                server.Start();

                LogThrottled($"ServerStart");

                while (true)
                {
                    // 클라이언트 연결을 받아들이기
                    _client = await server.AcceptTcpClientAsync();

                    LogThrottled($"Server Connected");
                    Tools.Log($"TCP Server Connected", Tools.ELogType.SystemLog);

                    // 연결된 클라이언트를 처리하는 메서드 호출
                    _ = HandleClientAsync();

                }
            }
            catch (Exception ex)
            {
                LogThrottled($"Error: {ex.Message}");
            }
            finally
            {
                // 서버 종료
                server?.Stop();
            }
        }





        private async Task HandleClientAsync()
        {
            try
            {
                NetworkStream stream = _client.GetStream();
                byte[] buffer = new byte[4096];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    //Tools.Log($"Receive Message: {receivedMessage}", Tools.ELogType.DisplayLog);



                    JObject jObject = JObject.Parse(receivedMessage);

                    if (jObject.ContainsKey("set_work") == true)
                    {
                        string parse_str = jObject["set_work"]["eventValue"].ToString();
                        _eventAggregator.GetEvent<IndicatorRecvEvent>().Publish(parse_str);
                    }


                    if (jObject.ContainsKey("send_backend") == true)
                    {
                        string parse_str = jObject["send_backend"]["eventValue"].ToString();
                        _eventAggregator.GetEvent<IndicatorRecvEvent>().Publish(parse_str);
                    }
                }
            }
            catch (Exception ex)
            {
                LogThrottled($"Error: {ex.Message}");
            }
            finally
            {
                _client.Close();
            }
        }

        private static void LogThrottled(string message)
        {
            var nowUtc = DateTime.UtcNow;
            if (_lastDisplayLogUtc == DateTime.MinValue || (nowUtc - _lastDisplayLogUtc) >= TimeSpan.FromSeconds(1))
            {
                if (_displayLogSuppressed > 0)
                {
                    Tools.Log($"{message} (+{_displayLogSuppressed} suppressed)", Tools.ELogType.DisplayLog);
                }
                else
                {
                    Tools.Log(message, Tools.ELogType.DisplayLog);
                }
                _lastDisplayLogUtc = nowUtc;
                _displayLogSuppressed = 0;
            }
            else
            {
                _displayLogSuppressed++;
            }
        }

    }

}
