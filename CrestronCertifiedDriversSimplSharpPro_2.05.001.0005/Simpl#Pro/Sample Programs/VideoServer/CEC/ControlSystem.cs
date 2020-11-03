using System;
using System.Linq;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.DeviceTypes.VideoServer;
using Crestron.RAD.ProTransports;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
// For Threading
// For Threading
// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
// For System Monitor Acces
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharpPro.DM.Cards;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharp.CrestronIO;
using RadVideoServerCecExample;

namespace CEC
{
    public class ControlSystem : CrestronControlSystem
    {
        // Panel settings
        private readonly uint _panelIpId = 0x03;
        private readonly string _panelSgdFilename = Directory.GetApplicationDirectory() + "\\CCD VideoServer Cec Example Project.sgd";
        private readonly string _driverFilename = "\\User\\CecAmazonFireTv.dll";

        // Driver settings
        public ABasicVideoServer VideoServerDriver;
        public CecTransport CecTransport;

        // Driver and panel
        private XpanelForSmartGraphics _panel;


        public ControlSystem()
            :base()
        { }

        public override void InitializeSystem()
        {
            try
            {
                CrestronConsole.AddNewConsoleCommand(CreateDriver, "TestCec", string.Empty, ConsoleAccessLevelEnum.AccessProgrammer);
                CreatePanel(_panelIpId, _panelSgdFilename);
                CreateDriver(_driverFilename);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        private void CreateDriver(string fileName)
        {
            try
            {
                if (this.SupportsSwitcherInputs)
                {
                    var hdmiInput = SwitcherInputs[1];
                    CrestronConsole.PrintLine("Input Type = {0}", hdmiInput.CardInputOutputType);
                    if (hdmiInput.CardInputOutputType == eCardInputOutputType.Dmps3HdmiInput)
                    {
                        var cecStream = ((Card.Dmps3HdmiInput)hdmiInput).HdmiInputPort.StreamCec;
                        CecTransport = new CecTransport();
                        CecTransport.Initialize(cecStream);
                        CecTransport.Start();
                        LoadAndInitializeDriver(fileName);
                    }
                    VideoServerDriver.StateChangeEvent += new Action<VideoServerStateObjects, IBasicVideoServer, byte>(VideoServerStateChange);
                    SetDefaultValues();
                }
                
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Exception in creating driver: {0}", e.Message);
                CrestronConsole.PrintLine("[Inner Exception] {0} : {1}", e.GetBaseException(), e.InnerException);
                CrestronConsole.PrintLine("[STACK]: {0}", e.StackTrace);
            }
            finally
            {
                CrestronConsole.PrintLine("Driver Created");
            }
        }

        private void CreatePanel(uint ipId, string sgdFilename)
        {
            _panel = new XpanelForSmartGraphics(ipId, this);
            _panel.LoadSmartObjects(sgdFilename);
            _panel.SmartObjects[(uint)PanelSmartObjects.DPad].SigChange += new SmartObjectSigChangeEventHandler(VideoServerDpadChange);
            _panel.SigChange += new SigEventHandler(PanelSigChange);
            _panel.Register();
        }

        void VideoServerStateChange(VideoServerStateObjects state, IBasicVideoServer device, byte arg3)
        {
            switch (state)
            {
                case VideoServerStateObjects.Connection:
                    _panel.StringInput[(uint)PanelSerialSignals.ConnectionFeedback].StringValue = device.Connected ? "Connected" : "Disconnected";
                    break;
            }
        }

        private void PanelSigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            if (VideoServerDriver == null)
            {
                return;
            }
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    {
                        if (Enum.IsDefined(typeof(PanelDigitalSignals), (int)args.Sig.Number))
                        {
                            PanelDigitalSignals signalNumber = (PanelDigitalSignals)args.Sig.Number;
                            if (args.Sig.BoolValue)
                            {
                                switch (signalNumber)
                                {
                                    case PanelDigitalSignals.ForwardScan:
                                        VideoServerDriver.ForwardScan();
                                        break;

                                    case PanelDigitalSignals.ForwardSkip:
                                        VideoServerDriver.ForwardSkip();
                                        break;

                                    case PanelDigitalSignals.Pause:
                                        VideoServerDriver.Pause();
                                        break;

                                    case PanelDigitalSignals.Play:
                                        VideoServerDriver.Play();
                                        break;

                                    case PanelDigitalSignals.ReverseScan:
                                        VideoServerDriver.ReverseScan();
                                        break;

                                    case PanelDigitalSignals.ReverseSkip:
                                        VideoServerDriver.ReverseSkip();
                                        break;

                                    case PanelDigitalSignals.Stop:
                                        VideoServerDriver.Stop();
                                        break;
                                    case PanelDigitalSignals.CustomCommand1:
                                        if (VideoServerDriver.CheckIfCustomCommandExists("CecGetVersion"))
                                        {
                                            CrestronConsole.PrintLine("CecGetVersion exists...");
                                            VideoServerDriver.SendCustomCommand("CecGetVersion");
                                        }
                                        else
                                        {
                                            CrestronConsole.PrintLine("CecGetVersion not available...");
                                        }
                                        
                                        break;
                                    case PanelDigitalSignals.Exit:
                                        VideoServerDriver.Exit();
                                        break;
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private void VideoServerDpadChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            if (VideoServerDriver != null)
            {
                if (args.Sig.BoolValue)
                {
                    switch (args.Sig.Number)
                    {
                        case 1:     // Up
                            VideoServerDriver.ArrowKey(ArrowDirections.Up, CommandAction.Hold);
                            break;
                        case 2:     // Down
                            VideoServerDriver.ArrowKey(ArrowDirections.Down, CommandAction.Hold);
                            break;
                        case 3:     // Left
                            VideoServerDriver.ArrowKey(ArrowDirections.Left, CommandAction.Hold);
                            break;
                        case 4:     // Right
                            VideoServerDriver.ArrowKey(ArrowDirections.Right, CommandAction.Hold);
                            break;
                        case 5:     // Center
                            VideoServerDriver.Select();
                            break;
                    }
                }
                else
                {
                    switch (args.Sig.Number)
                    {
                        case 1:     // Up
                            VideoServerDriver.ArrowKey(ArrowDirections.Up, CommandAction.Release);
                            break;
                        case 2:     // Down
                            VideoServerDriver.ArrowKey(ArrowDirections.Down, CommandAction.Release);
                            break;
                        case 3:     // Left
                            VideoServerDriver.ArrowKey(ArrowDirections.Left, CommandAction.Release);
                            break;
                        case 4:     // Right
                            VideoServerDriver.ArrowKey(ArrowDirections.Right, CommandAction.Release);
                            break;
                    }
                }
            }
        }

        private void SetDefaultValues()
        {
            if (VideoServerDriver != null)
            {
                // Update manufacturer and model feedback
                _panel.StringInput[(uint)PanelSerialSignals.ManufacturerFeedback].StringValue = VideoServerDriver.Manufacturer;
                _panel.StringInput[(uint)PanelSerialSignals.ModelFeedback].StringValue = VideoServerDriver.BaseModel;

                // Update digital enable joins for all buttons
                _panel.BooleanInput[(uint)PanelDigitalSignals.ForwardScan].BoolValue = VideoServerDriver.SupportsForwardScan;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ForwardSkip].BoolValue = VideoServerDriver.SupportsForwardSkip;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Pause].BoolValue = VideoServerDriver.SupportsPause;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Play].BoolValue = VideoServerDriver.SupportsPlay;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ReverseScan].BoolValue = VideoServerDriver.SupportsReverseScan;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ReverseSkip].BoolValue = VideoServerDriver.SupportsReverseSkip;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Stop].BoolValue = VideoServerDriver.SupportsStop;
                _panel.BooleanInput[(uint) PanelDigitalSignals.Home].BoolValue = VideoServerDriver.SupportsHome;
                _panel.BooleanInput[(uint) PanelDigitalSignals.CustomCommand1].BoolValue = true;
                _panel.BooleanInput[(uint) PanelDigitalSignals.Exit].BoolValue = VideoServerDriver.SupportsExit;
            }
        }

        private void LoadAndInitializeDriver(string fileName)
        {
            try
            {
                var dll = Assembly.LoadFrom(fileName);
                var types = dll.GetTypes();
 
                foreach (var cType in types)
                {
                    var interfaces = cType.GetInterfaces();
                    var cecDevice = interfaces.FirstOrDefault(x => x.Name.Equals("ICecDevice"));
                   
                    if (cecDevice != null)
                    {
                        VideoServerDriver = (ABasicVideoServer)dll.CreateInstance(cType.FullName);
                        ((ICecDevice)VideoServerDriver).Initialize(CecTransport);
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine(String.Format("Failure occured while loading the driver. Reason={0}", e.Message));
                CrestronConsole.PrintLine(e.StackTrace);
            }
        }
    }
}