using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharp.Net.Http;

// The HttpClientExample program allows the user to set up a monitor with the "monitor" console command that 
// consumes the REST API provided by a control system running the HttpCwsServerExample program. 
// The monitor will first GET the collection of relays, then use the server's Collection+JSON response to compute the
// total number of relays on the server control system. Next, the client will subscribe to a random relay
// from the collection by POSTing to its /web-hooks relative URL and listen for notifications indicating 
// that a state change has occurred on the relay. The monitor will print the updated relay state to the console. 
// Using the "stop" console command, the operator can cause the monitor to unsubscribe from the relay via a DELETE request
// and unregister the notification listener.

namespace HttpClientExample
{
    public class ControlSystem : CrestronControlSystem
    {
        // This RelayMonitor object encapsulates both the HttpClient making requests to the server
        // and the HttpCwsServer that listens for notifications. The user need only provide the hostname of the
        // server providing the REST API to begin monitoring a single relay remotely
        RelayMonitor monitor; 
        
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
                //Subscribe to the controller events (System and Program)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
            
                // Add Console Commands
                CrestronConsole.AddNewConsoleCommand(Monitor, "monitor", "Monitor a random relay using the server's REST API. usage: monitor <hostname>", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(Stop, "stop", "Stop the monitor client", ConsoleAccessLevelEnum.AccessOperator);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        // Monitor a random relay on the server control system.
        // This monitor can only subscribe to one relay at a time
        public void Monitor(string serverHostname)
        {
            try
            {
                if (monitor != null)
                {
                    CrestronConsole.ConsoleCommandResponse("Already subscribed: " + monitor + "\r\n");
                    return;
                }
                string myHostname = "";
                // adapterID 0 usually represents the outbound LAN port
                myHostname = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_HOSTNAME, 0).ToLower();
                
                monitor = new RelayMonitor(myHostname);
                monitor.Subscribe(serverHostname);

                CrestronConsole.ConsoleCommandResponse("Now monitoring: " + monitor);
            }
            catch (Exception e)
            {
                if (monitor != null)
                {
                    monitor.Dispose();
                    monitor = null;
                }
                CrestronConsole.ConsoleCommandResponse("Could not start client: {0}\r\n", e.Message);
            }
            finally
            {

            }
        }

        public void Stop(string args)
        {
            if (monitor == null)
            {
                CrestronConsole.ConsoleCommandResponse("Monitor already stopped\r\n");
                return;
            }
            monitor.Unsubscribe(); // sends a DELETE request to the server so it no longer will notify the monitor's listener URL
            monitor.Dispose();
            monitor = null;
            CrestronConsole.ConsoleCommandResponse("Monitor has been stopped\r\n"); 
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
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    if (monitor != null)
                    {
                        monitor.Dispose();
                        monitor = null;
                    }
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
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    if (monitor != null)
                    {
                        monitor.Dispose();
                        monitor = null;
                    }
                    break;
            }

        }
    }
}