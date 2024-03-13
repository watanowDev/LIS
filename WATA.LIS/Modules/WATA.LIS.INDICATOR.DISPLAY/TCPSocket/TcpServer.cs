
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WATA.LIS.TCPSocket
{ 
    class TcpServer : BaseSocket
    {
        Socket _passiveReceiveSocket;
        Thread _passiveTCPReceive;
        DispatcherTimer AliveTimer = new DispatcherTimer();
        public delegate void MsgEvent(byte[] msg);
        public event MsgEvent msg;

        private readonly IEventAggregator _eventAggregator;

        public TcpServer(IEventAggregator eventAggregator) : base()
        {
            this._eventAggregator = eventAggregator;
        }



        public override void Initialize(string ipaddr, int port)
        {
            try
            {

                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                if (port == 0)
                    port = 8000;

                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);

                _sock.Bind(ipEndPoint);
                _sock.Listen(1);

            }
            catch (SocketException se)
            {
                Console.WriteLine("SocketException = {0}", se.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Socket Initial Exception = {0}", e.Message);
            }

            _processThread = new Thread(new ParameterizedThreadStart(AcceptThreadHandler));
            _processThread.IsBackground = true;
            _processThread.Start();

        }
        /// <summary>
        /// start
        /// </summary>
        public override void Start()
        {

            //thread start
            _processThread.Start();
        }

        public override bool Send(byte[] msg)
        {
            try
            {
                if (_passiveReceiveSocket != null)
                {
                    if (_passiveReceiveSocket.Connected)
                    {
                        // Data Send
                        _passiveReceiveSocket.Send(msg);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);

            }
            finally
            {
                
            }

            return false;
        }

        /// <summary>
        /// stop
        /// </summary>
        public override void Stop()
        {
            _processThread.Abort();
            _sock.Shutdown(SocketShutdown.Both);
            _sock.Close();

            //throw new NotImplementedException();
        }

        public override void Disconnect()
        {
            if (_sock.Connected)
            {
                _sock.Shutdown(SocketShutdown.Both);
                _sock.Close();

               
            }
            //socket thread close
        }


        private void AcceptThreadHandler(object obj)
        {
            while (true)
            {
                try
                {
                    Socket receiveSocket = _sock.Accept();
                    
                    // Client Socket이 접속중일 경우 기존 접속 상태를 유지 시킨다.
                    if (_sock != null)
                    {
                        if (_sock.Connected)
                        {
                            //continue;
                            ServerDisconnect(_sock);

                            Console.WriteLine("Server Disconnected");
                            _sock = null;
                        }
                    }


                    // Local socket을 Global socket으로 전달
                    _passiveReceiveSocket = receiveSocket;

                    

                    if (receiveSocket != null)
                    {
                        //AsyncPostCallback(ConnectCallback);
                        // data receive Thread 실행
                        CreatePassiveSocketDataThread(receiveSocket);
                    }
                    else
                    {
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void CreatePassiveSocketDataThread(Socket socket)
        {
            _passiveTCPReceive = new Thread(new ParameterizedThreadStart(PassvieSocketDataReadHandler));
            _passiveTCPReceive.IsBackground = true;
            _passiveTCPReceive.Start(socket);
        }

        private void PassvieSocketDataReadHandler(object ob)
        {
            int rst = 0;
            Socket sock = (Socket)ob;
            SetKeepalive(sock);
            //DataGathering.Json.Parser.CimJsonParser parser = new DataGathering.Json.Parser.CimJsonParser();
            _passiveReceiveSocket = sock;

            while (true)
            {
                try
                {
                    int msgId_size = 10;
              
                    int size = 0;
                    byte[] arrMsgId = new byte[msgId_size];

                    int len = ReadTcpExact(sock, arrMsgId, msgId_size);

                   
                    if (arrMsgId[0] == 0)
                    {

                        //return;
                    }
                
                    if(size < 0)
                    {
                    
                    }


                    byte[] body = new byte[size];

                    len = ReadTcpExact(sock, body, body.Length);


                    if (len < 0)
                    {
                        return;
                    }

                    if(body[body.Length -1] == 69/*E*/)
                    {
                    }
                    else
                    {
                        return;
                    }

                    if (len != body.Length)
                    {
                        return;
                    }


                }
                catch (Exception e)
                {
                    Disconnect();
                    break;
                }
            }
        }
    }

}
