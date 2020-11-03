using System;
using System.Linq;
using Crestron.RAD.Drivers.Displays;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharp.CrestronIO;
using Crestron.RAD.Common.Interfaces;
using System.Text.RegularExpressions;
using Crestron.RAD.ProTransports;
using Crestron.RAD.Common.Enums;

namespace RadDisplayIrExample
{
    public class ControlSystem : CrestronControlSystem
    {
        public ControlSystem()
            : base()
        { }

        public override void InitializeSystem()
        {
            try
            {
                CreatePanel(_panelIpId, _panelSgdFilename);
                CreateDriver(_driverFilename, _deviceIrPortNumber);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        private void CreatePanel(uint ipId, string sgdFilename)
        {
            _panel = new XpanelForSmartGraphics(ipId, this);
            _panel.LoadSmartObjects(sgdFilename);
            _panel.SmartObjects[(uint)PanelSmartObjects.DisplayInputList].SigChange += new SmartObjectSigChangeEventHandler(DisplayInputListChange);
            _panel.SigChange += new SigEventHandler(PanelSigChange);
            _panel.Register();
        }

        private void CreateDriver(string filename, uint irPortNumber)
        {
            // IR Drivers use a singular driver
            _device = new IrDisplay();

            if (_device != null)
            {
                // Set up hardware
                var irOutputPort = IROutputPorts[irPortNumber];
                irOutputPort.Register();

                // Initialize the transport
                var irDriver = _device as IIr;
                var irTransport = new IrPortTransport(irOutputPort);
                irDriver.Initialize(irTransport, filename);

                // Set all default values for the VT Pro project
                SetDefaultValues();
            }
        }

        private void SetDefaultValues()
        {
            if (_device != null)
            {
                // Update manufacturer and model feedback
                _panel.StringInput[(uint)PanelSerialSignals.ManufacturerFeedback].StringValue = string.Empty;
                _panel.StringInput[(uint)PanelSerialSignals.ModelFeedback].StringValue = string.Empty;

                // Update digital enable joins for all buttons
                _panel.BooleanInput[(uint)PanelDigitalSignals.MuteOff].BoolValue = _device.SupportsDiscreteMute;
                _panel.BooleanInput[(uint)PanelDigitalSignals.MuteOn].BoolValue = _device.SupportsDiscreteMute;
                _panel.BooleanInput[(uint)PanelDigitalSignals.MuteToggle].BoolValue = _device.SupportsMute;
                _panel.BooleanInput[(uint)PanelDigitalSignals.PowerOff].BoolValue = _device.SupportsDiscretePower;
                _panel.BooleanInput[(uint)PanelDigitalSignals.PowerOn].BoolValue = _device.SupportsDiscretePower;
                _panel.BooleanInput[(uint)PanelDigitalSignals.PowerToggle].BoolValue = _device.SupportsTogglePower;
                _panel.BooleanInput[(uint)PanelDigitalSignals.VolMinus].BoolValue = _device.SupportsChangeVolume;
                _panel.BooleanInput[(uint)PanelDigitalSignals.VolPlus].BoolValue = _device.SupportsChangeVolume;

                // Add all display inputs to the smart object
                var inputDetails = _device.GetUsableInputs();
                var displayInputList = _panel.SmartObjects[(uint)PanelSmartObjects.DisplayInputList];
                displayInputList.UShortInput["Set Number of Items"].UShortValue = (ushort)inputDetails.Length;
                for (int i = 0; i < inputDetails.Length; i++)
                {
                    displayInputList.StringInput[String.Format("Set Item {0} Text", i + 1)].StringValue = inputDetails[i].Description;
                }
            }
        }

        private void PanelSigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            if (_device == null)
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
                                    case PanelDigitalSignals.MuteOff:
                                        _device.MuteOff();
                                        break;

                                    case PanelDigitalSignals.MuteOn:
                                        _device.MuteOn();
                                        break;

                                    case PanelDigitalSignals.MuteToggle:
                                        _device.Mute();
                                        break;

                                    case PanelDigitalSignals.PowerOff:
                                        _device.PowerOff();
                                        break;

                                    case PanelDigitalSignals.PowerOn:
                                        _device.PowerOn();
                                        break;

                                    case PanelDigitalSignals.PowerToggle:
                                        _device.PowerToggle();
                                        break;

                                    case PanelDigitalSignals.VolMinus:
                                        _device.VolumeDown(CommandAction.Hold);
                                        break;

                                    case PanelDigitalSignals.VolPlus:
                                        _device.VolumeUp(CommandAction.Hold);
                                        break;
                                }
                            }
                            else
                            {
                                switch (signalNumber)
                                {
                                    case PanelDigitalSignals.VolMinus:
                                        _device.VolumeDown(CommandAction.Release);
                                        break;
                                    case PanelDigitalSignals.VolPlus:
                                        _device.VolumeUp(CommandAction.Release);
                                        break;
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private void DisplayInputListChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            if (_device != null && args.Sig.BoolValue && args.Sig.Name.Contains("Pressed"))
            {
                int joinValue = Int32.Parse(Regex.Match(args.Sig.Name, @"\d+").Value);

                _device.SetInputSource(_device.GetUsableInputs().ElementAt(joinValue - 1).InputType);
            }
        }

        // Panel settings
        private readonly uint _panelIpId = 0x03;
        private readonly string _panelSgdFilename = Directory.GetApplicationDirectory() + "\\CCD Display IR Example Project.sgd";

        // Driver settings
        private readonly uint _deviceIrPortNumber = 1;
        private readonly string _driverFilename = Directory.GetApplicationDirectory() + "\\Driver.ir";

        // Driver and panel
        private XpanelForSmartGraphics _panel;
        private IrDisplay _device;
    }
}