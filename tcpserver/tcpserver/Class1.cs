using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronSockets;

namespace tcpserver
{
    public class MyTCPServer
    {
        public TCPServer server;
        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public MyTCPServer()
        {
            //constructor
        }

        public static void MyFunction(string myString)
        {
            //sample function
        }

        public void Init(string myString)
        {
            //init server
            server = new TCPServer("0.0.0.0", 9999, 4000, EthernetAdapterType.EthernetUnknownAdapter, 100);
            server.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(ServerSocketStatusChanged);
            server.WaitForConnectionAsync(ServerConnectedCallback);
        }

        public void ServerSocketStatusChanged(TCPServer server, uint clientIndex, SocketStatus status)
        {
        }

        public void ServerConnectedCallback(TCPServer server, uint clientIndex)
        {
            server.ReceiveDataAsync(clientIndex, Server_RecieveDataCallBack);
        }

        public void Server_RecieveDataCallBack(TCPServer server, uint clientIndex, int numberOfBytesReceived)
        {
            byte[] recvd_bytes = new byte[numberOfBytesReceived];
            Array.Copy(server.GetIncomingDataBufferForSpecificClient(clientIndex), recvd_bytes, numberOfBytesReceived);
            string recvd_msg = ASCIIEncoding.ASCII.GetString(recvd_bytes, 0, numberOfBytesReceived);
            CrestronConsole.PrintLine("Client " + clientIndex + " says: " + recvd_msg + "\r\nEchoing back to client " + clientIndex + "...");
            server.SendDataAsync(clientIndex, recvd_bytes, recvd_bytes.Length, ServerDataSentCallback);
            server.ReceiveDataAsync(clientIndex, Server_RecieveDataCallBack);
            CrestronConsole.PrintLine("---------- end of message ----------");
        }

        public void ServerDataSentCallback(TCPServer server, uint clientIndex, int bytesSent)
        {
        }
    }
}
