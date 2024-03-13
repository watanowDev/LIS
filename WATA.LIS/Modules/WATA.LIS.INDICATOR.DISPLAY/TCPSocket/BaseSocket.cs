using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WATA.LIS.TCPSocket
{
    abstract class BaseSocket
    {
        abstract public void Initialize(string ipaddr, int port);
        abstract public bool Send(byte[] msg);
        abstract public void Disconnect();

        abstract public void Start();
        abstract public void Stop();

        protected System.Net.Sockets.Socket _sock;
        protected Thread _processThread;

        protected int ReadTcpExact(Socket socket, byte[] buffer, int readSize)
        {
            int len = 0;
            int pos = 0;
            
            if (buffer.Length < readSize)
                return -1;

            while (readSize > 0)
            {

                if(buffer.Length < (pos + len))
                {
                    return -1;
                }

                len = socket.Receive(buffer, pos, readSize, 0);
                
                if (len <= 0)
                {
                    return len;
                }
                else
                {
                    pos += len;
                    readSize -= len;
                } 
            }

            return pos;
        }

        protected int ReadTcpExact(Socket socket, byte[] buffer)
        {
            return socket.Receive(buffer);
        }

        public unsafe void ServerDisconnect(Socket receiveSocket)
        {
            try
            {
                receiveSocket.Shutdown(SocketShutdown.Both);
                receiveSocket.Close();
            }
            catch (Exception e)
            {
                Console.Write("Socket Disconnect = Socket : {0}, Exception : {1}", receiveSocket.ToString(), e.Message);
            }
        }

        #region SocketOptionName
        /// <summary>
        /// SocketOptionName ReuseAddress 설정
        /// </summary>
        /// <param name="sSocket"></param>
        protected void SetReuseaddr(Socket sSocket)
        {
            sSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.ReuseAddress, true);
        }

        /// <summary>
        /// SocketOptionName NoDelay 설정
        /// </summary>
        /// <param name="sSocket"></param>
        protected void SetNodelay(Socket sSocket)
        {
            sSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
        }

        /// <summary>
        /// SocketOptionName keepAlive
        /// </summary>
        protected struct keepAlive
        {
            public int onoff;
            public int keepAliveTime;
            public int keepAliveInterval;

            public unsafe byte[] BufferKeepAlive
            {
                get
                {
                    var buf = new byte[sizeof(keepAlive)];
                    fixed (void* p = &this) Marshal.Copy(new IntPtr(p), buf, 0, buf.Length);
                    return buf;
                }
            }
        }
        /// <summary>
        /// SocketOptionName  KeepAlive 설정
        /// </summary>
        /// <param name="sSocket"></param>
        /// <param name="rSocket"></param>
        protected void SetKeepalive(Socket sSocket)
        {
            var ka = new keepAlive();
            ka.onoff = 1;
            ka.keepAliveTime = 3000;
            ka.keepAliveInterval = 1000;

            //sSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.KeepAlive, true);
            sSocket.IOControl(IOControlCode.KeepAliveValues, ka.BufferKeepAlive, null);
        }        
        #endregion

    }
}
