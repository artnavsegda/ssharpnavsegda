using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.GeneralIO;
using Crestron.SimplSharpPro.Keypads;

namespace office
{
    public class ControlSystem : CrestronControlSystem
    {
        private DinIo8 officeDinIo8;
        private C2nCbdP underShieldC2nCbdP, entranceC2nCbdP;
        private C2niCb meetingC2niCb;
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

                officeDinIo8 = new DinIo8(0x9, this);
                if (officeDinIo8.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine("Unable to register for officeDinIo8 ");
                    CrestronConsole.PrintLine("officeDinIo8 failed registration. Cause: {0}", officeDinIo8.RegistrationFailureReason);
                    ErrorLog.Error("officeDinIo8 failed registration. Cause: {0}", officeDinIo8.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine("officeDinIo8 successfully registered ");
                }

                underShieldC2nCbdP = new C2nCbdP(0x5, this);
                underShieldC2nCbdP.ButtonStateChange += new ButtonEventHandler(underShieldC2nCbdP_ButtonStateChange);
                if (underShieldC2nCbdP.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine("Unable to register for underShieldC2nCbdP ");
                    CrestronConsole.PrintLine("underShieldC2nCbdP failed registration. Cause: {0}", underShieldC2nCbdP.RegistrationFailureReason);
                    ErrorLog.Error("underShieldC2nCbdP failed registration. Cause: {0}", underShieldC2nCbdP.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine(" underShieldC2nCbdP successfully registered ");
                }

                entranceC2nCbdP = new C2nCbdP(0x4, this);
                entranceC2nCbdP.ButtonStateChange += new ButtonEventHandler(entranceC2nCbdP_ButtonStateChange);
                if (entranceC2nCbdP.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine("Unable to register for entranceC2nCbdP ");
                    CrestronConsole.PrintLine("entranceC2nCbdP failed registration. Cause: {0}", entranceC2nCbdP.RegistrationFailureReason);
                    ErrorLog.Error("entranceC2nCbdP failed registration. Cause: {0}", entranceC2nCbdP.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine("entranceC2nCbdP successfully registered ");
                }

                meetingC2niCb = new C2niCb(0x6, this);
                meetingC2niCb.ButtonStateChange += new ButtonEventHandler(meetingC2niCb_ButtonStateChange);
                if (meetingC2niCb.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine("Unable to register for meetingC2niCb ");
                    CrestronConsole.PrintLine("meetingC2niCb failed registration. Cause: {0}", meetingC2niCb.RegistrationFailureReason);
                    ErrorLog.Error("meetingC2niCb failed registration. Cause: {0}", meetingC2niCb.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine("meetingC2niCb successfully registered ");
                }

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
                officeDinIo8.VersiPorts[1].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                officeDinIo8.VersiPorts[2].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                officeDinIo8.VersiPorts[3].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                officeDinIo8.VersiPorts[4].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                officeDinIo8.VersiPorts[5].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                officeDinIo8.VersiPorts[6].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                officeDinIo8.VersiPorts[7].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                officeDinIo8.VersiPorts[8].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        void underShieldC2nCbdP_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            CrestronConsole.PrintLine("underShieldC2nCbdP Event sig: {0}, Type: {1}, State: {2}", args.Button.Number, args.Button.GetType(), args.Button.State);

            if (args.Button.State == eButtonState.Pressed)
            {
                switch (args.Button.Number)
                {
                    case 1:
                        officeDinIo8.VersiPorts[1].DigitalOut = !officeDinIo8.VersiPorts[1].DigitalOut;
                        underShieldC2nCbdP.Feedbacks[1].State = officeDinIo8.VersiPorts[1].DigitalOut;
                        entranceC2nCbdP.Feedbacks[1].State = officeDinIo8.VersiPorts[1].DigitalOut;
                        break;
                    case 4:
                        officeDinIo8.VersiPorts[3].DigitalOut = !officeDinIo8.VersiPorts[3].DigitalOut;
                        underShieldC2nCbdP.Feedbacks[4].State = officeDinIo8.VersiPorts[3].DigitalOut;
                        break;
                    default:
                        break;
                }
            }
        }

        void entranceC2nCbdP_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            CrestronConsole.PrintLine("entranceC2nCbdP Event sig: {0}, Type: {1}, State: {2}", args.Button.Number, args.Button.GetType(), args.Button.State);

            if (args.Button.State == eButtonState.Pressed)
            {
                switch (args.Button.Number)
                {
                    case 1:
                        officeDinIo8.VersiPorts[1].DigitalOut = !officeDinIo8.VersiPorts[1].DigitalOut;
                        underShieldC2nCbdP.Feedbacks[1].State = officeDinIo8.VersiPorts[1].DigitalOut;
                        entranceC2nCbdP.Feedbacks[1].State = officeDinIo8.VersiPorts[1].DigitalOut;
                        break;
                    case 2:
                        officeDinIo8.VersiPorts[2].DigitalOut = !officeDinIo8.VersiPorts[2].DigitalOut;
                        entranceC2nCbdP.Feedbacks[2].State = officeDinIo8.VersiPorts[2].DigitalOut;
                        meetingC2niCb.Feedbacks[1].State = officeDinIo8.VersiPorts[2].DigitalOut;
                        break;
                    case 6:
                        officeDinIo8.VersiPorts[1].DigitalOut = false;
                        officeDinIo8.VersiPorts[2].DigitalOut = false;
                        officeDinIo8.VersiPorts[3].DigitalOut = false;
                        officeDinIo8.VersiPorts[4].DigitalOut = false;
                        officeDinIo8.VersiPorts[5].DigitalOut = false;
                        officeDinIo8.VersiPorts[6].DigitalOut = false;
                        officeDinIo8.VersiPorts[7].DigitalOut = false;
                        officeDinIo8.VersiPorts[8].DigitalOut = false;

                        entranceC2nCbdP.Feedbacks[1].State = false;
                        entranceC2nCbdP.Feedbacks[2].State = false;
                        underShieldC2nCbdP.Feedbacks[1].State = false;
                        underShieldC2nCbdP.Feedbacks[4].State = false;
                        meetingC2niCb.Feedbacks[1].State = false;
                        meetingC2niCb.Feedbacks[4].State = false;
                        meetingC2niCb.Feedbacks[8].State = false;
                        meetingC2niCb.Feedbacks[10].State = false;
                        break;
                    default:
                        break;
                }
            }
        }

        void meetingC2niCb_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            CrestronConsole.PrintLine("meetingC2niCb Event sig: {0}, Type: {1}, State: {2}", args.Button.Number, args.Button.GetType(), args.Button.State);

            if (args.Button.State == eButtonState.Pressed)
            {
                switch (args.Button.Number)
                {
                    case 1:
                        officeDinIo8.VersiPorts[2].DigitalOut = !officeDinIo8.VersiPorts[2].DigitalOut;
                        entranceC2nCbdP.Feedbacks[2].State = officeDinIo8.VersiPorts[2].DigitalOut;
                        meetingC2niCb.Feedbacks[1].State = officeDinIo8.VersiPorts[2].DigitalOut;
                        break;
                    case 4:
                        officeDinIo8.VersiPorts[5].DigitalOut = !officeDinIo8.VersiPorts[5].DigitalOut;
                        meetingC2niCb.Feedbacks[4].State = officeDinIo8.VersiPorts[5].DigitalOut;
                        break;
                    case 8:
                        officeDinIo8.VersiPorts[6].DigitalOut = !officeDinIo8.VersiPorts[6].DigitalOut;
                        meetingC2niCb.Feedbacks[8].State = officeDinIo8.VersiPorts[6].DigitalOut;
                        break;
                    case 10:
                        officeDinIo8.VersiPorts[4].DigitalOut = !officeDinIo8.VersiPorts[4].DigitalOut;
                        meetingC2niCb.Feedbacks[10].State = officeDinIo8.VersiPorts[4].DigitalOut;
                        break;
                    default:
                        break;
                }
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