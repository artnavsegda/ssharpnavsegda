using System;
using Crestron.RAD.Drivers.CableBoxes;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharp.CrestronIO;
using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.ProTransports;
using Crestron.RAD.Common.Enums;

namespace RadTvTunerIRExample
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
            _panel.SmartObjects[(uint)PanelSmartObjects.DPad].SigChange += new SmartObjectSigChangeEventHandler(CableBoxDPadChange);
            _panel.SigChange += new SigEventHandler(PanelSigChange);
            _panel.Register();
        }

        private void CreateDriver(string filename, uint irPortNumber)
        {
            // IR Drivers use a singular driver
            _device = new IrCableBox();

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
                _panel.BooleanInput[(uint)PanelDigitalSignals.ChanMinus].BoolValue = _device.SupportsChangeChannel;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ChanPlus].BoolValue = _device.SupportsChangeChannel;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ForwardScan].BoolValue = _device.SupportsForwardScan;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ForwardSkip].BoolValue = _device.SupportsForwardSkip;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Pause].BoolValue = _device.SupportsPause;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Play].BoolValue = _device.SupportsPlay;
                _panel.BooleanInput[(uint)PanelDigitalSignals.PowerOff].BoolValue = _device.SupportsDiscretePower;
                _panel.BooleanInput[(uint)PanelDigitalSignals.PowerOn].BoolValue = _device.SupportsDiscretePower;
                _panel.BooleanInput[(uint)PanelDigitalSignals.PowerToggle].BoolValue = _device.SupportsTogglePower;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ReverseScan].BoolValue = _device.SupportsReverseScan;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ReverseSkip].BoolValue = _device.SupportsReverseSkip;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Stop].BoolValue = _device.SupportsStop;
                _panel.BooleanInput[(uint)PanelDigitalSignals.VolMinus].BoolValue = _device.SupportsChangeVolume;
                _panel.BooleanInput[(uint)PanelDigitalSignals.VolPlus].BoolValue = _device.SupportsChangeVolume;
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
                                    case PanelDigitalSignals.ChanMinus:
                                        _device.ChannelDown(CommandAction.Hold);
                                        break;

                                    case PanelDigitalSignals.ChanPlus:
                                        _device.ChannelUp(CommandAction.Hold);
                                        break;

                                    case PanelDigitalSignals.ForwardScan:
                                        _device.ForwardScan();
                                        break;

                                    case PanelDigitalSignals.ForwardSkip:
                                        _device.ForwardSkip();
                                        break;

                                    case PanelDigitalSignals.Pause:
                                        _device.Pause();
                                        break;

                                    case PanelDigitalSignals.Play:
                                        _device.Play();
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

                                    case PanelDigitalSignals.ReverseScan:
                                        _device.ReverseScan();
                                        break;

                                    case PanelDigitalSignals.ReverseSkip:
                                        _device.ReverseSkip();
                                        break;

                                    case PanelDigitalSignals.Stop:
                                        _device.Stop();
                                        break;

                                    case PanelDigitalSignals.VolMinus:
                                        _device.VolumeDown(CommandAction.Hold);
                                        break;

                                    case PanelDigitalSignals.VolPlus:
                                        _device.VolumeUp(CommandAction.Hold);
                                        break;
                                    case PanelDigitalSignals.Key0:
                                        _device.KeypadNumber(0);
                                        break;
                                    case PanelDigitalSignals.Key1:
                                        _device.KeypadNumber(1);
                                        break;
                                    case PanelDigitalSignals.Key2:
                                        _device.KeypadNumber(2);
                                        break;
                                    case PanelDigitalSignals.Key3:
                                        _device.KeypadNumber(3);
                                        break;
                                    case PanelDigitalSignals.Key4:
                                        _device.KeypadNumber(4);
                                        break;
                                    case PanelDigitalSignals.Key5:
                                        _device.KeypadNumber(5);
                                        break;
                                    case PanelDigitalSignals.Key6:
                                        _device.KeypadNumber(6);
                                        break;
                                    case PanelDigitalSignals.Key7:
                                        _device.KeypadNumber(7);
                                        break;
                                    case PanelDigitalSignals.Key8:
                                        _device.KeypadNumber(8);
                                        break;
                                    case PanelDigitalSignals.Key9:
                                        _device.KeypadNumber(9);
                                        break;
                                    case PanelDigitalSignals.KeyDash:
                                        _device.Dash();
                                        break;
                                    case PanelDigitalSignals.KeyEnter:
                                        _device.Enter();
                                        break;
                                }
                            }
                            else
                            {
                                switch (signalNumber)
                                {
                                    case PanelDigitalSignals.ChanMinus:
                                        _device.ChannelDown(CommandAction.Release);
                                        break;

                                    case PanelDigitalSignals.ChanPlus:
                                        _device.ChannelUp(CommandAction.Release);
                                        break;

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

        private void CableBoxDPadChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            if (_device != null)
            {
                if (args.Sig.BoolValue)
                {
                    switch (args.Sig.Number)
                    {
                        case 1:     // Up
                            _device.ArrowKey(ArrowDirections.Up, CommandAction.Hold);
                            break;
                        case 2:     // Down
                            _device.ArrowKey(ArrowDirections.Down, CommandAction.Hold);
                            break;
                        case 3:     // Left
                            _device.ArrowKey(ArrowDirections.Left, CommandAction.Hold);
                            break;
                        case 4:     // Right
                            _device.ArrowKey(ArrowDirections.Right, CommandAction.Hold);
                            break;
                        case 5:     // Center
                            _device.Enter();
                            break;
                    }
                }
                else
                {
                    switch (args.Sig.Number)
                    {
                        case 1:     // Up
                            _device.ArrowKey(ArrowDirections.Up, CommandAction.Release);
                            break;
                        case 2:     // Down
                            _device.ArrowKey(ArrowDirections.Down, CommandAction.Release);
                            break;
                        case 3:     // Left
                            _device.ArrowKey(ArrowDirections.Left, CommandAction.Release);
                            break;
                        case 4:     // Right
                            _device.ArrowKey(ArrowDirections.Right, CommandAction.Release);
                            break;
                    }
                }
            }
        }

        // Panel settings
        private readonly uint _panelIpId = 0x03;
        private readonly string _panelSgdFilename = Directory.GetApplicationDirectory() + "\\CCD TVTuner IR Example Project.sgd";

        // Driver settings
        private readonly uint _deviceIrPortNumber = 1;
        private readonly string _driverFilename = Directory.GetApplicationDirectory() + "\\Driver.ir";

        // Driver and panel
        private XpanelForSmartGraphics _panel;
        private IrCableBox _device;
    }
}