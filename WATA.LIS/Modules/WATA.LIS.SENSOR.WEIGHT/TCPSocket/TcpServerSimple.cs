
using Newtonsoft.Json;
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
using System.Xml.Linq;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Model.VISION;
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

        public TcpServerSimple(IEventAggregator eventAggregator) 
        {
            this._eventAggregator = eventAggregator;

           // _eventAggregator.GetEvent<IndicatorSendEvent>().Subscribe(onSendMessageAsync, ThreadOption.BackgroundThread, true);

        }

        public async void onSendMessageAsync(string SendMessage)
        {
            if(_client == null)
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

                    Tools.Log($"{SendMessage}", Tools.ELogType.WeightLog);

                }

            }
            catch (Exception ex)
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
                int port = 7382;

                // TcpListener 인스턴스 생성
                server = new TcpListener(ipAddress, port);

                // 클라이언트 연결 대기
                server.Start();
                
                Tools.Log($"ServerStart", Tools.ELogType.WeightLog);

                while (true)
                {
                    // 클라이언트 연결을 받아들이기
                    _client = await server.AcceptTcpClientAsync();

                    Tools.Log($"Server Connected", Tools.ELogType.WeightLog);

                    // 연결된 클라이언트를 처리하는 메서드 호출
                    _ = HandleClientAsync();

                   
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Error: {ex.Message}", Tools.ELogType.WeightLog);
            }
            finally
            {
                // 서버 종료
                server?.Stop();
            }
        }

        public static bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            if ((strInput.StartsWith("{") && strInput.EndsWith("}")) || (strInput.StartsWith("[") && strInput.EndsWith("]")))
            {
                try
                {
                    JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException)
                {
                    return false;
                }
                catch (Exception) // 다른 예외도 처리 가능합니다.
                {
                    return false;
                }
            }
            else
            {
                return false;
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
                    Tools.Log($"Receive Message: {receivedMessage}", Tools.ELogType.WeightLog);

                    Tools.Log($"length {receivedMessage.Length}", Tools.ELogType.WeightLog);

                    
                    if (receivedMessage.Length > 70)
                    {
                        Tools.Log($"Json Length Error", Tools.ELogType.WeightLog);

                        return;
                    }

                    if (IsValidJson(receivedMessage))
                    {

                        Tools.Log($"Is Json ##", Tools.ELogType.WeightLog);



                    }
                    else
                    {
                        Tools.Log($"Json Error", Tools.ELogType.WeightLog);

                        return;
                    }


                    JObject jObject = JObject.Parse(receivedMessage);

                    if (jObject.ContainsKey("weightSensor") == true)
                    {


                    }
                    else
                    {
                        Tools.Log($"Parse Error", Tools.ELogType.WeightLog);

                        return;


                    }



                    int nGrossWeight = (int)jObject["weightSensor"]["GrossWeight"];
                    int nRightWeight = (int)jObject["weightSensor"]["RightWeight"];
                    int nLeftWeight = (int)jObject["weightSensor"]["LeftWeight"];



                    WeightSensorModel model = new WeightSensorModel();
                    model.GrossWeight = nGrossWeight;
                    model.RightWeight = nRightWeight;
                    model.LeftWeight = nLeftWeight;

                    Tools.Log($"nGrossWeight : {nGrossWeight}", Tools.ELogType.WeightLog);
                    Tools.Log($"nRightWeight : {nRightWeight}", Tools.ELogType.WeightLog);
                    Tools.Log($"nLeftWeight : {nLeftWeight}", Tools.ELogType.WeightLog);


                    _eventAggregator.GetEvent<WeightSensorEvent>().Publish(model);

                }
            }
            catch (Exception ex)
            {
          
                Tools.Log($"Error: {ex.Message}", Tools.ELogType.WeightLog);
            }
            finally
            {
                _client.Close();
               
            }
        }


    }

}
