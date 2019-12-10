using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronSockets;

namespace tcpserver
{
    public class MyTCPServer
    {
        static TCPServer server;
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
            server = new TCPServer(9999, 100);
        }
    }
}
