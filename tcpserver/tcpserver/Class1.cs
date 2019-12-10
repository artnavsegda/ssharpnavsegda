using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronSockets;

namespace tcpserver
{
    public class MyTCPServer
    {
        private TCPServer server;
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
            server = new TCPServer(9999, 100);
            server.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(ServerSocketStatusChanged);
        }

        void ServerSocketStatusChanged(TCPServer server, uint clientIndex, SocketStatus status)
        {
        }
    }
}
