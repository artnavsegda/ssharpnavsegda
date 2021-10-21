using System;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.Drivers.BlurayPlayers;
using Crestron.RAD.ProTransports;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharp.CrestronIO;

namespace RadBlurayPlayerIrExample
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
            _panel.SmartObjects[(uint)PanelSmartObjects.DPad].SigChange += new SmartObjectSigChangeEventHandler(BlurayPlayerDpadChange);
            _panel.SigChange += new SigEventHandler(PanelSigChange);
            _panel.Register();
        }

        private void CreateDriver(string filename, uint irPortNumber)
        {
            // IR Drivers use a singular driver
            _device = new IrBlurayPlayer();

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
                _panel.StringInput[(uint)PanelSerialSignals.ManufacturerFeedback].StringValue = _device.Manufacturer;
                _panel.StringInput[(uint)PanelSerialSignals.ModelFeedback].StringValue = _device.BaseModel;

                // Update digital enable joins for all buttons
                _panel.BooleanInput[(uint)PanelDigitalSignals.Audio].BoolValue = _device.SupportsAudio;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Display].BoolValue = _device.SupportsDisplay;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Eject].BoolValue = _device.SupportsEject;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Options].BoolValue = _device.SupportsOptions;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Subtitle].BoolValue = _device.SupportsSubtitle;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ForwardScan].BoolValue = _device.SupportsForwardScan;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ForwardSkip].BoolValue = _device.SupportsForwardSkip;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Pause].BoolValue = _device.SupportsPause;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Play].BoolValue = _device.SupportsPlay;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ReverseScan].BoolValue = _device.SupportsReverseScan;
                _panel.BooleanInput[(uint)PanelDigitalSignals.ReverseSkip].BoolValue = _device.SupportsReverseSkip;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Stop].BoolValue = _device.SupportsStop;
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
                                    case PanelDigitalSignals.Audio:
                                        _device.Audio();
                                        break;

                                    case PanelDigitalSignals.Display:
                                        _device.Display();
                                        break;

                                    case PanelDigitalSignals.Eject:
                                        _device.Eject();
                                        break;

                                    case PanelDigitalSignals.Options:
                                        _device.Options();
                                        break;

                                    case PanelDigitalSignals.Subtitle:
                                        _device.Subtitle();
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

                                    case PanelDigitalSignals.ReverseScan:
                                        _device.ReverseScan();
                                        break;

                                    case PanelDigitalSignals.ReverseSkip:
                                        _device.ReverseSkip();
                                        break;

                                    case PanelDigitalSignals.Stop:
                                        _device.Stop();
                                        break;

                                }
                            }
                        }
                    }
                    break;
            }
        }

        private void BlurayPlayerDpadChange(GenericBase currentDevice, SmartObjectEventArgs args)
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
        private readonly string _panelSgdFilename = Directory.GetApplicationDirectory() + "\\CCD BlurayPlayer IR Example Project.sgd";

        // Driver settings
        private readonly uint _deviceIrPortNumber = 1;
        private readonly string _driverFilename = Directory.GetApplicationDirectory() + "\\Driver.ir";
        //private readonly string _driverFilename = Directory.GetApplicationDirectory() + "\\Driver.pkg";

        // Driver and panel
        private XpanelForSmartGraphics _panel;
        private IrBlurayPlayer _device;
    }
}