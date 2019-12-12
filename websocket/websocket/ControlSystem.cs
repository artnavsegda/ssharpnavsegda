using System;
using System.Text;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support

namespace websocket
{
    public class ControlSystem : CrestronControlSystem
    {
        private TCPServer server;

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem()
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            try
            {
                server = new TCPServer("0.0.0.0", 9876, 4000, EthernetAdapterType.EthernetUnknownAdapter, 1000);
                SocketErrorCodes err = server.WaitForConnectionAsync(ServerConnectedCallback);
                CrestronConsole.PrintLine("WaitForConnectionAsync returned: " + err);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        public void ServerConnectedCallback(TCPServer server, uint clientIndex)
        {
            if (clientIndex != 0)
            {
                CrestronConsole.PrintLine("Server listening on port " + server.PortNumber + " has connected with a client (client #" + clientIndex + ")");
                server.ReceiveDataAsync(clientIndex, ServerDataReceivedCallback);
                if (server.MaxNumberOfClientSupported == server.NumberOfClientsConnected)
                {
                    CrestronConsole.PrintLine("Client limit reached.");
                    // This call to Stop() causes the server.State flag, SERVER_NOT_LISTENING, to be set
                    server.Stop();
                    CrestronConsole.PrintLine("After calling server.Stop(), the server state is: " + server.State);
                }
                // If the client limit is reached, WaitForConnectionAsync will return SOCKET_MAX_CONNECTIONS_REACHED
                // and the ServerConnectedCallback will not be registered. Otherwise, the call to WaitForConnectionAsync
                // causes the server to keep listening for more clients.
                SocketErrorCodes err = server.WaitForConnectionAsync(ServerConnectedCallback);
                CrestronConsole.PrintLine("WaitForConnectionAsync returned: " + err);
            }
            // A clientIndex of 0 could mean that the server is no longer listening, or that the TLS handshake failed when a client tried to connect.
            // In the case of a TLS handshake failure, wait for another connection so that other clients can still connect
            else
            {
                CrestronConsole.Print("Error in ServerConnectedCallback: ");
                if ((server.State & ServerState.SERVER_NOT_LISTENING) > 0)
                {
                    CrestronConsole.PrintLine("Server is no longer listening.");
                }
                else
                {
                    CrestronConsole.PrintLine("Unable to make connection with client.");
                    // This connection failed, but keep waiting for another
                    server.WaitForConnectionAsync(ServerConnectedCallback);
                }
            }
        }

        public void ServerDataReceivedCallback(TCPServer server, uint clientIndex, int bytesReceived)
        {
            if (bytesReceived <= 0)
            {
                CrestronConsole.PrintLine("ServerDataReceivedCallback error: server's connection with client " + clientIndex + " has been closed.");
                server.Disconnect(clientIndex);
                // A connection has closed, so another client may connect if the server stopped listening 
                // due to the maximum number of clients connecting
                if ((server.State & ServerState.SERVER_NOT_LISTENING) > 0)
                    server.WaitForConnectionAsync(ServerConnectedCallback);
            }
            else
            {
                CrestronConsole.PrintLine("\n------ incoming message -----------");
                byte[] recvd_bytes = new byte[bytesReceived];

                // Copy the received bytes into a local buffer so that they can be echoed back.
                // Do not pass the reference to the incoming data buffer itself to the SendDataAsync method
                Array.Copy(server.GetIncomingDataBufferForSpecificClient(clientIndex), recvd_bytes, bytesReceived);

                // The server in this example expects ASCII text from the client, but any other encoding is possible
                string recvd_msg = ASCIIEncoding.ASCII.GetString(recvd_bytes, 0, bytesReceived);
                CrestronConsole.PrintLine("Client " + clientIndex + " says: " + recvd_msg + "\r\nEchoing back to client " + clientIndex + "...");

                // echo the received message back to the client who sent it
                server.SendDataAsync(clientIndex, recvd_bytes, recvd_bytes.Length, ServerDataSentCallback);

                // Begin waiting for another message from that same client
                server.ReceiveDataAsync(clientIndex, ServerDataReceivedCallback);

                CrestronConsole.PrintLine("---------- end of message ----------");
            }
        }

        public void ServerDataSentCallback(TCPServer server, uint clientIndex, int bytesSent)
        {
            if (bytesSent <= 0)
            {
                CrestronConsole.PrintLine("Error sending message. Connection has been closed");
            }
            else
            {
                CrestronConsole.PrintLine("Echoed message to client " + clientIndex + " (" + bytesSent + " byte(s))");
            }
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }
    }
}