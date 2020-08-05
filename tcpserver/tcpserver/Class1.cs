using System;
using System.Collections;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronSockets;

namespace tcpserver
{
    public class Server
    {
        private TCPServer _server;
        private ArrayList _clientList;
        private bool _waiting;
        private uint _numberOfClientsConnected;

        // default constructor
        public Server()
        {
            _clientList = new ArrayList();
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
            foreach (uint clientIndex in _clientList)
            {
                if (_server.ClientConnected(clientIndex) && dataToSend != null && dataToSend.Length > 0)
                {
                    _server.SendDataAsync(clientIndex, Encoding.ASCII.GetBytes(dataToSend), dataToSend.Length, Server_SendDataCallback);
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
                _numberOfClientsConnected++;
                _clientList.Add(clientIndex);

                // check if needing to wait for a new connection
                this.CheckForWaitingConnection();


                s.ReceiveDataAsync(clientIndex, Server_RecieveDataCallBack);
                /*
                _numberOfClientsConnected--;
                CrestronConsole.PrintLine("Server> Disconnect {0}", clientIndex);

                if (!_waiting)
                    this.CheckForWaitingConnection();
                 */
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

        private void Server_RecieveDataCallBack(TCPServer s, uint clientIndex, int numberOfBytesReceived)
        {
            CrestronConsole.PrintLine("Server> NumberOfBytesReceived[{0}] from Client[{1}]", numberOfBytesReceived, clientIndex);
            if (numberOfBytesReceived > 0)
            {
#if Debug
                CrestronConsole.PrintLine("ReceiveDataCallBack: client: [{0}] length: [{1}]",clientIndex,numberOfBytesReceived);
#endif
                //_serverOnDataReceived(null, new ServerTcpReceiveEventArgs(clientIndex, s.GetIncomingDataBufferForSpecificClient(clientIndex).Take(numberOfBytesReceived).ToString()));
                while (s.ClientConnected(clientIndex))
                {
                    numberOfBytesReceived = s.ReceiveData(clientIndex);
                    if (numberOfBytesReceived > 0)
                    {
#if Debug
                CrestronConsole.PrintLine("ReceiveDataCallBack: client: [{0}] length: [{1}]",clientIndex,numberOfBytesReceived);
#endif
                        //_serverOnDataReceived(null, new ServerTcpReceiveEventArgs(clientIndex, s.GetIncomingDataBufferForSpecificClient(clientIndex).Take(numberOfBytesReceived).ToString()));
                    }
                    else
                    {
                        s.ReceiveDataAsync(clientIndex, Server_RecieveDataCallBack);
                        break;
                    }
                }
            }
        }
    }
}
