using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support

// The HttpCwsServerExample program causes the control system to serve a REST API to allow clients to monitor and 
// update the control system's relays using HTTP requests. This programs follows the RESTful WebHooks standard 
// (found at https://webhooks.pbworks.com/w/page/13385128/RESTful%20WebHooks) so clients can subscribe to 
// state change events on the relays and receive notifications when one opens or closes

namespace HttpCwsServerExample
{
    public class ControlSystem : CrestronControlSystem
    {
        RestfulRelayServer serv;
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

                CrestronConsole.AddNewConsoleCommand(StartServer, "startserver", "start the CWS Relay server", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(RelayOpen, "rlyopen", "usage: rlyopen <relayID>", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(RelayClose, "rlyclose", "usage: rlyclose <relayID>", ConsoleAccessLevelEnum.AccessOperator);

            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        public void RelayOpen(string arg)
        {
            try
            {
                uint ID = uint.Parse(arg);
                RelayPorts[ID].Open();
            }
            catch
            {
                CrestronConsole.ConsoleCommandResponse("Input error");
            }
        }

        public void RelayClose(string arg)
        {
            try
            {
                uint ID = uint.Parse(arg);
                RelayPorts[ID].Close();
            }
            catch
            {
                CrestronConsole.ConsoleCommandResponse("Input error");
            }
        }

        public void StartServer(string args)
        {
            try
            {
                if (this.SupportsRelay)
                {
                    if (RegisterRelays())
                    {
                        string hostname = "";
                        // adapterID 0 usually represents the outbound LAN port
                        hostname = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_HOSTNAME, 0).ToLower();
                        if (serv != null) serv.Dispose();
                        serv = new RestfulRelayServer(RelayPorts, hostname);
                        CrestronConsole.ConsoleCommandResponse("Now serving REST API rooted at http://" + hostname + "/cws/api for the control system's " +
                            NumberOfRelayPorts + " relay ports.");
                    }
                    else
                    {
                        CrestronConsole.ConsoleCommandResponse("Could not start server: unable to register all relays");
                    }
                }
                else
                {
                    CrestronConsole.ConsoleCommandResponse("This control system does not support relays, so the Relay API could not be created");
                }
            }
            catch (Exception e)
            {
                CrestronConsole.ConsoleCommandResponse("Error in startserver user command: " + e.Message);
            }
            finally {

            }
        }      

        public bool RegisterRelays()
        {
            foreach (Relay r in RelayPorts)
            {
                if (!r.Registered)
                {
                    if (r.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    {
                        CrestronConsole.ConsoleCommandResponse("Failed to register relay #" + r.ID +
                            ": " + r.DeviceRegistrationFailureReason);
                        return false;
                    }
                }
            }
            return true;
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
                    serv.Dispose();
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
                    serv.Dispose();
                    break;
            }

        }
    }
}