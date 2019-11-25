using System;
using System.Text;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.Keypads;
using Crestron.SimplSharpPro.Lighting;
using Crestron.SimplSharpPro.Lighting.Din;

namespace SIMPLSharpProgram1
{
    public class ControlSystem : CrestronControlSystem
    {
        private C2nCbdP myKeypad;
        private C2niCb myKeypad2;
        private Din1DimU4 myDimmer;
        private Din8Sw8i myRelay;

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

                //register device
                myKeypad = new C2nCbdP(0x25, this);
                myKeypad.ButtonStateChange += myKeypad_ButtonStateChange;

                if (myKeypad.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine(" Unable to register for keypad ");
                    CrestronConsole.PrintLine("myKeypad {0} failed registration. Cause: {1}", 0x25, myKeypad.RegistrationFailureReason);
                    ErrorLog.Error("myKeypad {0} failed registration. Cause: {1}", 0x25, myKeypad.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine(" Keypad successfully registered ");
                }

                myKeypad2 = new C2niCb(0x26, this);
                myKeypad2.ButtonStateChange += myKeypad2_ButtonStateChange;

                if (myKeypad2.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine(" Unable to register for keypad2 ");
                    CrestronConsole.PrintLine("myKeypad2 {0} failed registration. Cause: {1}", 0x26, myKeypad2.RegistrationFailureReason);
                    ErrorLog.Error("myKeypad2 {0} failed registration. Cause: {1}", 0x26, myKeypad2.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine(" Keypad2 successfully registered ");
                }

                myDimmer = new Din1DimU4(0x0A, this);
                // Now register the event handlers
                myDimmer.OverrideEventHandler += OverrideEventHandler;
                myDimmer.OnlineStatusChange += OnlineStatusChangeCallback;
                myDimmer.BaseEvent += BaseEventCallback;

                // Need to set parameters before we register the device
                foreach (DinDimmableLoad dimmableLoad in myDimmer.DinLoads)
                {
                    dimmableLoad.ParameterDimmable = eDimmable.No;
                }
                // Register the device now
                if (myDimmer.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine(" Unable to register for dimmer ");
                    CrestronConsole.PrintLine("myDimmer {0} failed registration. Cause: {1}", 0x0A, myDimmer.RegistrationFailureReason);
                    ErrorLog.Error("myDimmer {0} failed registration. Cause: {1}", 0x0A, myDimmer.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine(" Dimmer successfully registered ");
                }

                myRelay = new Din8Sw8i(0x92, this);
                myRelay.OverrideEventHandler += relayOverrideEventHandler;
                myRelay.OnlineStatusChange += relayOnlineStatusChangeCallback;
                myRelay.BaseEvent += relayBaseEventCallback;
                myRelay.LoadStateChange += LoadEventCallback;

                // Register the device now
                if (myRelay.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine(" Unable to register for relay ");
                    CrestronConsole.PrintLine("myRelay {0} failed registration. Cause: {1}", 0x92, myRelay.RegistrationFailureReason);
                    ErrorLog.Error("myRelay {0} failed registration. Cause: {1}", 0x92, myRelay.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine(" Relay successfully registered ");
                }

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += ControlSystem_ControllerSystemEventHandler;
                CrestronEnvironment.ProgramStatusEventHandler += ControlSystem_ControllerProgramEventHandler;
                CrestronEnvironment.EthernetEventHandler += ControlSystem_ControllerEthernetEventHandler;

                //my console command
                CrestronConsole.AddNewConsoleCommand(Hello, "hello", "hello command", ConsoleAccessLevelEnum.AccessOperator);

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
            CrestronConsole.PrintLine("Hello form SSharp program");
            try
            {
                
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        // My Console Command Delegate
        void Hello(string response)
        {
            CrestronConsole.ConsoleCommandResponse("Hello world: {0}", response.ToUpper());
        }


        // Keypad event handler
        void myKeypad_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            var btn = args.Button;
            var uo = btn.UserObject;

            CrestronConsole.PrintLine("Event sig: {0}, Type: {1}, State: {2}", btn.Number, btn.GetType(), btn.State);

            if (btn.State == eButtonState.Pressed)
            {
                switch (btn.Number)
                {
                    case 1:
                        if (myDimmer.DinLoads[2].LevelOut.UShortValue > 0)
                        {
                            myDimmer.DinLoads[2].LevelIn.UShortValue = SimplSharpDeviceHelper.PercentToUshort(0f);
                            myKeypad.Feedbacks[1].State = false;
                        }
                        else
                        {
                            myDimmer.DinLoads[2].LevelIn.UShortValue = SimplSharpDeviceHelper.PercentToUshort(100f);
                            myKeypad.Feedbacks[1].State = true;
                        }
                        break;
                    case 2:
                        if (myDimmer.DinLoads[3].LevelOut.UShortValue > 0)
                        {
                            myDimmer.DinLoads[3].LevelIn.UShortValue = SimplSharpDeviceHelper.PercentToUshort(0f);
                            myKeypad.Feedbacks[2].State = false;
                        }
                        else
                        {
                            myDimmer.DinLoads[3].LevelIn.UShortValue = SimplSharpDeviceHelper.PercentToUshort(100f);
                            myKeypad.Feedbacks[2].State = true;
                        }
                        break;
                    case 5:
                        myRelay.SwitchedLoads[1].FullOn();
                        myRelay.SwitchedLoads[2].FullOff();
                        break;
                    case 6:
                        myRelay.SwitchedLoads[2].FullOn();
                        myRelay.SwitchedLoads[1].FullOff();
                        break;
                    case 7:
                        myRelay.SwitchedLoads[3].FullOn();
                        myRelay.SwitchedLoads[4].FullOff();
                        break;
                    case 8:
                        myRelay.SwitchedLoads[4].FullOn();
                        myRelay.SwitchedLoads[3].FullOff();
                        break;
                    default:
                        break;
                }
            }

        }

        // Keypad event handler
        void myKeypad2_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            var btn = args.Button;
            var uo = btn.UserObject;

            CrestronConsole.PrintLine("Event keypad 2 sig: {0}, Type: {1}, State: {2}", btn.Number, btn.GetType(), btn.State);

            if (btn.State == eButtonState.Pressed)
            {
                switch (btn.Number)
                {
                    case 2:
                        if (myDimmer.DinLoads[1].LevelOut.UShortValue > 0)
                        {
                            myDimmer.DinLoads[1].LevelIn.UShortValue = SimplSharpDeviceHelper.PercentToUshort(0f);
                            myKeypad2.Feedbacks[1].State = false;
                            myKeypad2.Feedbacks[2].State = false;
                        }
                        else
                        {
                            myDimmer.DinLoads[1].LevelIn.UShortValue = SimplSharpDeviceHelper.PercentToUshort(100f);
                            myKeypad2.Feedbacks[1].State = true;
                            myKeypad2.Feedbacks[2].State = true;
                        }
                        break;
                    case 9:
                        myRelay.SwitchedLoads[5].FullOn();
                        myRelay.SwitchedLoads[6].FullOff();
                        break;
                    case 11:
                        myRelay.SwitchedLoads[6].FullOn();
                        myRelay.SwitchedLoads[5].FullOff();
                        break;
                    default:
                        break;
                }
            }

        }

        // Online Offline Status Change Event Handler 
        void OnlineStatusChangeCallback(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            CrestronConsole.PrintLine(" Din1DimU4 state {0} ", args.DeviceOnLine ? "Online" : "Offline");
        }

        // Online Offline Status Change Event Handler 
        void relayOnlineStatusChangeCallback(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            CrestronConsole.PrintLine(" Din8Sw8i  state {0} ", args.DeviceOnLine ? "Online" : "Offline");
        }

        // Base Event handler for the Din1DimU4
        void BaseEventCallback(GenericBase device, BaseEventArgs args)
        {
            CrestronConsole.PrintLine("dimmer {0}", args.EventId);
            switch (args.EventId)
            {
                case DinLoadBaseClass.LevelOutFeedbackEventId:
                    CrestronConsole.PrintLine(" Level out changed for output {0} ", args.Index);
                    CrestronConsole.PrintLine(" Value of load is {0} ", myDimmer.DinLoads[(uint)args.Index].LevelOut.UShortValue);
                    //myDimmer.DinLoads[(uint)args.Index].LevelIn.UShortValue = myDimmer.DinLoads[(uint)args.Index].LevelOut.UShortValue;
                    break;
                case DinLoadBaseClass.NoAcPowerFeedbackEventId:
                    CrestronConsole.PrintLine(" NoAcPowerFeedbackEventId fired. State is {0} ", myDimmer.NoAcPower.BoolValue);
                    break;
            }
        }

        void relayBaseEventCallback(GenericBase device, BaseEventArgs args)
        {
            CrestronConsole.PrintLine("relay {0}", args.EventId);
            switch (args.EventId)
            {
                //case DinLoadBaseClass.LevelOutFeedbackEventId:
                //    CrestronConsole.PrintLine(" relay Level out changed for output {0} ", args.Index);
                //    //CrestronConsole.PrintLine(" Value of load is {0} ", myRelay.DinLoads[(uint)args.Index].LevelOut.UShortValue);
                //    break;
                //case DinLoadBaseClass.NoAcPowerFeedbackEventId:
                //    CrestronConsole.PrintLine(" NoAcPowerFeedbackEventId fired. State is {0} ", myRelay.NoAcPower.BoolValue);
                //    break;
            }
        }

        void LoadEventCallback(LightingBase lightingObject, LoadEventArgs args)
        {
            CrestronConsole.PrintLine("load relay {0}", args.EventId);

            // use this structure to react to the different events
            switch (args.EventId)
            {
                case LoadEventIds.IsOnEventId:
                    CrestronConsole.PrintLine(" relay Level out changed for output {0} ", args.Load.Number);
                    CrestronConsole.PrintLine(" Value of load is {0} ", myRelay.SwitchedLoads[args.Load.Number].IsOn);
                    //xp.BooleanInput[1].BoolValue = !lampDimmer.DimmingLoads[1].IsOn;
                    //xp.BooleanInput[2].BoolValue = lampDimmer.DimmingLoads[1].IsOn;
                    break;

                case LoadEventIds.LevelChangeEventId:
                    //xp.UShortInput[1].UShortValue = lampDimmer.DimmingLoads[1].LevelFeedback.UShortValue;
                    break;

                case LoadEventIds.LevelInputChangedEventId:
                    //xp.UShortInput[1].CreateRamp(lampDimmer.DimmingLoads[1].Level.RampingInformation);
                    break;

                default:
                    break;
            }
        }

        // Override handler for the Din1DimU4
        void OverrideEventHandler(GenericBase device, eOverrideEvents overrideEvent)
        {
            switch (overrideEvent)
            {
                case eOverrideEvents.External:
                    CrestronConsole.PrintLine(" Device  reports external override event. State is {0} ", myDimmer.InExternalOverrideFeedback.BoolValue);
                    break;
                case eOverrideEvents.Manual:
                    CrestronConsole.PrintLine(" Device  reports manual override event. State is {0} ", myDimmer.InManualOverrideFeedback.BoolValue);
                    break;
            }
        }

        // Override handler for the Din1DimU4
        void relayOverrideEventHandler(GenericBase device, eOverrideEvents overrideEvent)
        {
            switch (overrideEvent)
            {
                case eOverrideEvents.External:
                    CrestronConsole.PrintLine(" Device relay reports external override event. State is {0} ", myRelay.InExternalOverrideFeedback.BoolValue);
                    break;
                case eOverrideEvents.Manual:
                    CrestronConsole.PrintLine(" Device relay reports manual override event. State is {0} ", myRelay.InManualOverrideFeedback.BoolValue);
                    break;
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