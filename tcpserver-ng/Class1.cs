using System;
using System.Collections;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace tcpserver
{
    public delegate void DelegateServerTcpReceive(SimplSharpString data);

    public class Connection
    {
        //private TCPServer _server;
        public readonly uint ClientIndex;
        private int _totalBytesReceived = 0;

        private void Server_RecieveDataCallBack(TCPServer s, uint newClientIndex, int numberOfBytesReceived)
        {
//            CrestronConsole.PrintLine("Server> NumberOfBytesReceived[{0}] from Client[{1}]", numberOfBytesReceived, newClientIndex);
            if (numberOfBytesReceived > 0)
            {
                _totalBytesReceived += numberOfBytesReceived;
//#if Debug
                CrestronConsole.PrintLine("ReceiveDataCallBack: client: [{0}] length: [{1}]", newClientIndex, numberOfBytesReceived);
//#endif
                byte[] recvd_bytes = new byte[numberOfBytesReceived];
                Array.Copy(s.GetIncomingDataBufferForSpecificClient(newClientIndex), recvd_bytes, numberOfBytesReceived);
                //ServerTcpReceive(ASCIIEncoding.ASCII.GetString(recvd_bytes, 0, numberOfBytesReceived));
                while (s.ClientConnected(newClientIndex))
                {
                    numberOfBytesReceived = s.ReceiveData(newClientIndex);
                    if (numberOfBytesReceived > 0)
                    {
                        _totalBytesReceived += numberOfBytesReceived;
//#if Debug
                        CrestronConsole.PrintLine("ReceiveDataCallBack: client: [{0}] length: [{1}]", newClientIndex, numberOfBytesReceived);
//#endif
                        recvd_bytes = new byte[numberOfBytesReceived];
                        Array.Copy(s.GetIncomingDataBufferForSpecificClient(newClientIndex), recvd_bytes, numberOfBytesReceived);
                        //ServerTcpReceive(ASCIIEncoding.ASCII.GetString(recvd_bytes, 0, numberOfBytesReceived));
                    }
                    else
                    {
                        s.ReceiveDataAsync(newClientIndex, Server_RecieveDataCallBack);
                        break;
                    }
                }
            }
        }

        public Connection(TCPServer newServer, uint newClientIndex)
        {
            ClientIndex = newClientIndex;
            //_server = newServer;
            newServer.ReceiveDataAsync(ClientIndex, Server_RecieveDataCallBack);
        }

        public void Close()
        {
            CrestronConsole.PrintLine("Connection destructed, total bytes recieved [{0}]", _totalBytesReceived);
        }

        ~Connection()
        {
            CrestronConsole.PrintLine("Connection destructed, total bytes recieved [{0}]", _totalBytesReceived);
        }
    }

    public class Server
    {
        private TCPServer _server;
        private Hashtable _clientList;
        private bool _waiting;
        private uint _numberOfClientsConnected;

        public DelegateServerTcpReceive ServerTcpReceive { get; set; }

        // default constructor
        public Server()
        {
            _clientList = new Hashtable();
            _numberOfClientsConnected = 0;
        }

        public void Init(int port, int numberOfConnections)
        {
            try
            {
                CrestronConsole.PrintLine("Initializing Server on port {0} with {1} connections allowed.", port, numberOfConnections);
                _server = new TCPServer("0.0.0.0", port, 4000, EthernetAdapterType.EthernetUnknownAdapter, numberOfConnections);

                _server.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(_server_SocketStatusChange);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("Error starting server in init: {0}", ex.Message);
            }

            SocketErrorCodes error = _server.WaitForConnectionAsync("0.0.0.0", Server_ClientConnectCallback);
            CrestronConsole.PrintLine("Server> WaitingForConnection[{0}] [{1}]", error, _server.MaxNumberOfClientSupported);
            _waiting = (error == SocketErrorCodes.SOCKET_OPERATION_PENDING);
        }

        void _server_SocketStatusChange(TCPServer myTCPServer, uint clientIndex, SocketStatus serverSocketStatus)
        {
            if (serverSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                _numberOfClientsConnected--;
                (_clientList[clientIndex] as Connection).Close();
                _clientList.Remove(clientIndex);
                CrestronConsole.PrintLine("Server> Disconnect {0}", clientIndex);

                if (!_waiting)
                    this.CheckForWaitingConnection();
            }

            //_serverSocketStatus(null, new ServerTcpSocketStatusEventArgs(clientIndex, serverSocketStatus, _numberOfClientsConnected));
        }

        public void ServerSendData(uint clientIndex, string dataToSend)
        {
            if (_server.ClientConnected(clientIndex) && dataToSend != null && dataToSend.Length > 0)
            {
                _server.SendDataAsync(clientIndex, Encoding.ASCII.GetBytes(dataToSend), dataToSend.Length, Server_SendDataCallback);
            }
        }

        public void ServerSendDataToEveryone(string dataToSend)
        {
            foreach (DictionaryEntry client in _clientList)
            {
                if (_server.ClientConnected((client.Value as Connection).ClientIndex) && dataToSend != null && dataToSend.Length > 0)
                {
                    _server.SendDataAsync((client.Value as Connection).ClientIndex, Encoding.ASCII.GetBytes(dataToSend), dataToSend.Length, Server_SendDataCallback);
                }
            }
        }

        private void ServerClose(uint clientIndex)
        {
            _server.Disconnect(clientIndex);
        }

        private void CheckForWaitingConnection()
        {
            if (_numberOfClientsConnected < _server.MaxNumberOfClientSupported)
            {
                SocketErrorCodes error = _server.WaitForConnectionAsync("0.0.0.0", Server_ClientConnectCallback);
                CrestronConsole.PrintLine("Server> WaitingForConnection[{0}] [{1}]", error, _server.MaxNumberOfClientSupported);
                _waiting = (error == SocketErrorCodes.SOCKET_OPERATION_PENDING);
            }
            else
            {
                CrestronConsole.PrintLine("Server> No more connections available.");
                _waiting = false;
            }
        }

        private void Server_ClientConnectCallback(TCPServer s, uint clientIndex)
        {
            CrestronConsole.PrintLine("Server> Incoming Connection: [{0}|{1}/{2}] :: SocketStatus: {3}", clientIndex, _numberOfClientsConnected, s.MaxNumberOfClientSupported, s.GetServerSocketStatusForSpecificClient(clientIndex));

            if (s.ClientConnected(clientIndex))
            {
                Connection newConnection = new Connection(s, clientIndex);

                _numberOfClientsConnected++;
                _clientList.Add(clientIndex, newConnection);

                // check if needing to wait for a new connection
                this.CheckForWaitingConnection();
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
    }
}
