// Decompiled with JetBrains decompiler
// Type: WebsocketServer.WebsocketSrvr
// Assembly: WebsocketServer, Version=1.0.0.27676, Culture=neutral, PublicKeyToken=null
// MVID: 9416C711-8AFD-444F-9A24-232C417E9E26
// Assembly location: C:\Users\artna\Desktop\WebsocketServer.dll

using System;
using System.Collections;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace WebsocketServer
{
    public delegate void StringCallback(SimplSharpString msg);

    public class Connection
    {
        private TCPServer _server;
        public readonly uint ClientIndex;
        private int _totalBytesReceived = 0;
        private ByteBuffer myBuffer;
        private bool isOnline = false;

        static public StringCallback RecieveMessage { get; set; }

        private void tcpServerReceiveCallback(TCPServer myTCPServer, uint clientIndex, int numberOfBytesReceived)
        {
            if (numberOfBytesReceived > 0)
            {
                this.myBuffer.Append(myTCPServer.GetIncomingDataBufferForSpecificClient(clientIndex), numberOfBytesReceived);
                if (!this.isOnline)
                {
                    if (this.myBuffer.IndexOf("\r\n\r\n") > 0)
                    {
                        string boundString = StringUtil.getBoundString(this.myBuffer.ToString(), "Sec-WebSocket-Key: ", "\r\n");
                        if (boundString != null && boundString.Length > 0)
                        {
                            byte[] numArray = WebsocketUtil.buildHandshakeMessage(boundString);
                            myTCPServer.SendData(clientIndex, numArray, numArray.Length);
                            this.myBuffer.Clear();
                            this.isOnline = true;
                        }
                        else
                            this.resetConnection(clientIndex);
                    }
                }
                else
                {
                    while (true)
                    {
                        uint num1 = WebsocketUtil.DecodeLength(this.myBuffer.ToArray());
                        if ((long)this.myBuffer.Length() >= (long)num1 && num1 != 0U)
                        {
                            byte[] array = this.myBuffer.ToArray(0, (int)num1);
                            this.myBuffer.Delete(0, (int)num1);
                            byte num2 = array[0];
                            byte[] numArray1 = WebsocketUtil.DecodeMsg(array);
                            switch (num2)
                            {
                                case 129:
                                    string msg = StringUtil.toString(numArray1);
                                    if (RecieveMessage != null)
                                        RecieveMessage(msg);
                                    CrestronConsole.PrintLine("Connection {0}, recieved [{1}]", ClientIndex, msg);
                                    break;
                                case 136:
                                    this.resetConnection(clientIndex);
                                    break;
                                case 137:
                                    byte[] numArray2 = WebsocketUtil.EncodeMsg((byte)138, numArray1);
                                    myTCPServer.SendData(clientIndex, numArray2, numArray2.Length);
                                    break;
                            }
                        }
                        else
                            break;
                    }
                }
                this._server.ReceiveDataAsync(clientIndex, new TCPServerReceiveCallback(tcpServerReceiveCallback));
            }
        }

        private void resetConnection(uint clientIndex)
        {
            this._server.Disconnect(clientIndex);
            this.myBuffer.Clear();
            this.isOnline = false;
        }

        public Connection(TCPServer newServer, uint newClientIndex)
        {
            this.myBuffer = new ByteBuffer();
            ClientIndex = newClientIndex;
            _server = newServer;
            newServer.ReceiveDataAsync(ClientIndex, tcpServerReceiveCallback);
        }

        public Connection()
        {
            
        }

        public void Close()
        {
            CrestronConsole.PrintLine("Connection {0}, total bytes recieved [{1}]", ClientIndex, _totalBytesReceived);
        }
    }

    public class WebsocketSrvr
    {
        private bool restartConnection = false;
        
        private TCPServer server;
        private Hashtable _clientList;
        private bool _waiting;

        public WebsocketSrvr()
        {
            _clientList = new Hashtable();
        }

        public void Initialize(int port)
        {
            if (port <= 0)
                return;
            this.server = new TCPServer("0.0.0.0", port, 1024, EthernetAdapterType.EthernetUnknownAdapter, 100);
            this.server.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(_server_SocketStatusChange);
            SocketErrorCodes error = this.server.WaitForConnectionAsync(tcpServerClientConnectCallback);
            _waiting = (error == SocketErrorCodes.SOCKET_OPERATION_PENDING);
        }

        public void ServerSendDataToEveryone(string dataToSend)
        {
            foreach (Connection client in _clientList)
            {
                if (server.ClientConnected(client.ClientIndex) && dataToSend != null && dataToSend.Length > 0)
                {
                    byte[] numArray = WebsocketUtil.EncodeMsg((byte)129, StringUtil.toByteArray(dataToSend));
                    server.SendDataAsync(client.ClientIndex, numArray, numArray.Length, Server_SendDataCallback);
                }
            }
        }

        private void Server_SendDataCallback(TCPServer s, uint clientIndex, int numberOfBytesSent)
        {
            CrestronConsole.PrintLine("Server> NumberOfBytesSent[{0}] to Client[{1}]", numberOfBytesSent, clientIndex);
            if (numberOfBytesSent == -1)
            {
                CrestronConsole.PrintLine("Client[{0}] socket closed", clientIndex);
                _clientList.Remove(clientIndex);
            }
        }

        private void CheckForWaitingConnection()
        {
            SocketErrorCodes error = this.server.WaitForConnectionAsync(tcpServerClientConnectCallback);
            CrestronConsole.PrintLine("Server> WaitingForConnection[{0}] [{1}]", error, this.server.MaxNumberOfClientSupported);
            _waiting = (error == SocketErrorCodes.SOCKET_OPERATION_PENDING);
        }

        private void tcpServerClientConnectCallback(TCPServer myTCPServer, uint clientIndex)
        {
            CrestronConsole.PrintLine("Server> Incoming Connection: [{0}] :: SocketStatus: {1}", clientIndex, myTCPServer.GetServerSocketStatusForSpecificClient(clientIndex));

            if (myTCPServer.ClientConnected(clientIndex))
            {
                Connection newConnection = new Connection(myTCPServer, clientIndex);

                _clientList.Add(clientIndex, newConnection);

                // check if needing to wait for a new connection
                this.CheckForWaitingConnection();
            }
        }

        void _server_SocketStatusChange(TCPServer myTCPServer, uint clientIndex, SocketStatus serverSocketStatus)
        {
            if (serverSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                _clientList.Remove(clientIndex);
                CrestronConsole.PrintLine("Server> Disconnect {0}", clientIndex);

                if (!_waiting)
                    this.CheckForWaitingConnection();
            }
        }
    }
}
