using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.CrestronIO;

namespace WebSocketServer
{
    #region 状态 委托
    public delegate void LogEventHandler(string Msg);
    /// <summary>
    /// 服务器状态 未连接，等待连接，连接已建立
    /// </summary>
    public enum ServerStatusLevel { Off, WaitingConnection, ConnectionEstablished };
    /// <summary>
    /// 连接建立委托
    /// </summary>
    /// <param name="e"></param>
    public delegate void NewConnectionEventHandler(EventArgs e);
    /// <summary>
    /// 数据接收委托
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    /// <param name="e"></param>
    public delegate void DataReceivedEventHandler(Object sender, string message, EventArgs e);
    /// <summary>
    /// 连接断开委托
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void DisconnectedEventHandler(Object sender, EventArgs e);
    // public delegate void BroadcastEventHandler(string message, EventArgs e);

    #endregion

    public class WebSocketServer : IDisposable
    {
        /// <summary>
        /// 是否已经销毁
        /// </summary>
        private bool AlreadyDisposed;
        /// <summary>
        /// TCP服务
        /// </summary>
        private TCPServer Listener;
        /// <summary>
        /// 最大连接数
        /// </summary>
        private int ConnectionsQueueLength;
        /// <summary>
        /// 最大缓冲区大小
        /// </summary>
        private int MaxBufferSize;
        private byte[] FirstByte;
        private byte[] LastByte;
        /// <summary>
        /// 客户端列表
        /// </summary>
        List<SocketConnection> connectionSocketList = new List<SocketConnection>();

        public ServerStatusLevel Status { get; private set; }
        /// <summary>
        /// 端口号
        /// </summary>
        public int ServerPort { get; set; }

       // public string ConnectionOrigin { get; set; }

        public event NewConnectionEventHandler NewConnection;
        public event DataReceivedEventHandler DataReceived;
        public event DisconnectedEventHandler Disconnected;
        public event LogEventHandler Log;
        private void Initialize()
        {
            AlreadyDisposed = false;

            Status = ServerStatusLevel.Off;
            ConnectionsQueueLength = 20;
            MaxBufferSize = 1024 * 100;
            FirstByte = new byte[MaxBufferSize];
            LastByte = new byte[MaxBufferSize];
            FirstByte[0] = 0x00;
            LastByte[0] = 0xFF;
            
        }

        public WebSocketServer()
        {
            //设置服务端端口号
            ServerPort = 7001;
            Initialize();

        }

        public WebSocketServer(int serverPort)
        {
            ServerPort = serverPort;
            //ConnectionOrigin = connectionOrigin;
            Initialize();
        }


        ~WebSocketServer()
        {
            Close();
        }


        public void Dispose()
        {
            Close();
        }

        private void Close()
        {
            if (!AlreadyDisposed)
            {
                AlreadyDisposed = true;
                if (Listener != null) Listener.Stop();
                foreach (SocketConnection item in connectionSocketList)
                {
                    item.ConnectionSocket.Stop();
                }
                connectionSocketList.Clear();
            }
        }

        public static IPAddress getLocalmachineIPAddress()
        {
            string strHostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);

            foreach (IPAddress ip in ipEntry.AddressList)
            {
                //IPV4
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;
            }

            return ipEntry.AddressList[0];
        }

        /// <summary>
        /// 启动WebSocket服务 启动TCP连接，并监听客户端连接 连接数量20
        /// </summary>
        public void StartServer()
        {

            Listener = new TCPServer("0.0.0.0", ServerPort, MaxBufferSize, EthernetAdapterType.EthernetLANAdapter, ConnectionsQueueLength);

            Listener.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(Listener_SocketStatusChange);

            //WebSocketAddress

            while (true)
            {
                //While Wait Connect
                try
                {
                    SocketErrorCodes codes = Listener.WaitForConnection("0.0.0.0",this.OnClientConnect);
                }
                catch (Exception ex)
                {
                    if (this.Log!=null)
                    {
                        this.Log(ex.Message);
                    }
                   // logger.Log(ex.Message);
                }
            }
        }

        public void OnClientConnect(TCPServer myTCPServer, uint clientIndex)
        {
            if (myTCPServer.ClientConnected(clientIndex))
            {
                SocketConnection socketConn = new SocketConnection();
                socketConn.clientindex = clientIndex;
                socketConn.ConnectionSocket = myTCPServer;
                socketConn.NewConnection += new NewConnectionEventHandler(socketConn_NewConnection);
                socketConn.DataReceived += new DataReceivedEventHandler(socketConn_BroadcastMessage);
                socketConn.Disconnected += new DisconnectedEventHandler(socketConn_Disconnected);
                socketConn.Log += this.Log;

                myTCPServer.ReceiveDataAsync(clientIndex, socketConn.ManageHandshake, 0);


                connectionSocketList.Add(socketConn);
                //ClientConnected clientIndex
            }
            this.Log(string.Format("Client Index {0} Connected", clientIndex));

        }
        /// <summary>
        /// 网络状态变化
        /// </summary>
        /// <param name="myTCPServer"></param>
        /// <param name="clientIndex"></param>
        /// <param name="serverSocketStatus"></param>
        void Listener_SocketStatusChange(TCPServer myTCPServer, uint clientIndex, SocketStatus serverSocketStatus)
        {
            this.Log(string.Format("SocketStatusChange:{0}", serverSocketStatus));

            if (serverSocketStatus==SocketStatus.SOCKET_STATUS_NO_CONNECT)
            {
                for (int i = 0; i < connectionSocketList.Count; i++)
                {
                    if (connectionSocketList[i].clientindex == clientIndex)
                    {
                        connectionSocketList[i].SocketClose();
                        connectionSocketList.Remove(connectionSocketList[i]);
                    }
                }
            }

            //logger.Log("SocketStatusChange:" + serverSocketStatus);
            //throw new NotImplementedException();
        }
        void socketConn_Disconnected(Object sender, EventArgs e)
        {
            this.Log(string.Format("socketConn_Disconnected"));

            if (Disconnected != null)
                Disconnected(sender, e);

            SocketConnection sConn = sender as SocketConnection;
            if (sConn != null)
            {
               // Send(string.Format("{0} DisConnected", sConn.Name));
                sConn.SocketClose();
                connectionSocketList.Remove(sConn);
            }
        }

        void socketConn_BroadcastMessage(Object sender, string message, EventArgs e)
        {
         //   logger.Log("ClientMsg:" + message);
            if (DataReceived!=null)
            {
                this.DataReceived(sender, message, e);
            }
           // Send("ServerMsg:"+DateTime.Now.ToLongTimeString());
        }

        void socketConn_NewConnection(EventArgs e)
        {
            if (NewConnection != null)
                NewConnection(EventArgs.Empty);
        }

        public void Send(string message)
        {
            foreach (SocketConnection item in connectionSocketList)
            {
                if (!item.ConnectionSocket.ClientConnected(item.clientindex)) return;
                try
                {
                    this.Log(string.Format("CP3WebSocketSend:{0} {1}", item.clientindex,message));

                    if (item.IsDataMasked)
                    {
                        DataFrame dr = new DataFrame(message);
                        item.ConnectionSocket.SendData(item.clientindex, dr.GetBytes(), dr.GetBytes().Length);
                    }
                    else
                    {
                        item.ConnectionSocket.SendData(item.clientindex, FirstByte, FirstByte.Length);

                        item.ConnectionSocket.SendData(item.clientindex, Encoding.GetEncoding(28591).GetBytes(message), Encoding.GetEncoding(28591).GetBytes(message).Length);
                        item.ConnectionSocket.SendData(item.clientindex, LastByte, LastByte.Length);
                    }
                }
                catch (Exception ex)
                {
                    this.Log(string.Format("Exception:{0}", ex.Message));

                  //  logger.Log(ex.Message);
                }
            }
        }
    }
}
