using System;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Reflection;
using Crestron.RAD.Common.Interfaces;
using System.Text.RegularExpressions;
using Crestron.RAD.Common.Enums;

namespace RadDisplayEthernetExample
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
                CreateDriver(_driverFilename, _deviceIpAddress);
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

        private void CreateDriver(string filename, string ipAddress)
        {
            // Use reflection to get the driver
            _device = GetAssembly<IBasicVideoDisplay>(filename, "IBasicVideoDisplay", "ITcp");

            if (_device != null)
            {
                // Set the device ID
                _device.Id = _deviceId;

                // Initialize the transport
                ((ITcp)_device).Initialize(IPAddress.Parse(ipAddress), ((ITcp)_device).Port);

                // Suscribe to events
                _device.StateChangeEvent += new Action<DisplayStateObjects, IBasicVideoDisplay, byte>(DisplayStateChange);

                // Set all default values for the VT Pro project
                SetDefaultValues();

                // Connect the driver to the device
                _device.Connect();
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
                _panel.BooleanInput[(uint)PanelDigitalSignals.Connect].BoolValue = true;
                _panel.BooleanInput[(uint)PanelDigitalSignals.Disconnect].BoolValue = _device.SupportsDisconnect;
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

        private void DisplayStateChange(DisplayStateObjects state, IBasicVideoDisplay device, byte arg3)
        {
            switch (state)
            {
                case DisplayStateObjects.Audio:
                    _panel.UShortInput[(uint)PanelAnalogSignals.VolumeValue].UShortValue = (ushort)device.VolumePercent;
                    _panel.StringInput[(uint)PanelSerialSignals.MuteFeedback].StringValue = device.Muted ? "On" : "Off";
                    break;

                case DisplayStateObjects.Connection:
                    _panel.StringInput[(uint)PanelSerialSignals.ConnectionFeedback].StringValue = device.Connected ? "Connected" : "Disconnected";
                    break;

                case DisplayStateObjects.Input:
                    _panel.StringInput[(uint)PanelSerialSignals.InputFeedback].StringValue = device.InputSource.Description;
                    break;

                case DisplayStateObjects.Power:
                    _panel.StringInput[(uint)PanelSerialSignals.PowerFeedback].StringValue = device.PowerIsOn ? "On" : "Off";
                    break;
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
                                    case PanelDigitalSignals.Connect:
                                        _device.Connect();
                                        break;

                                    case PanelDigitalSignals.Disconnect:
                                        _device.Disconnect();
                                        break;

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

        private T GetAssembly<T>(string fileName, string deviceInterfaceName, string deviceTransportName)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(deviceInterfaceName) || string.IsNullOrEmpty(deviceTransportName))
            {
                return default(T);
            }

            var dll = Assembly.LoadFrom(fileName);
            var types = dll.GetTypes();

            foreach (var cType in types)
            {
                var interfaces = cType.GetInterfaces();

                if (interfaces.FirstOrDefault(x => x.Name == deviceInterfaceName) == null)
                {
                    continue;
                }
                if (interfaces.FirstOrDefault(x => x.Name == deviceTransportName) != null)
                {
                    return (T)dll.CreateInstance(cType.FullName);
                }
            }
            return default(T);
        }

        // Panel settings
        private readonly uint _panelIpId = 0x03;
        private readonly string _panelSgdFilename = Directory.GetApplicationDirectory() + "\\CCD Display Ethernet Example Project.sgd";

        // Driver settings
        private readonly byte _deviceId = 1;
        private readonly string _deviceIpAddress = "0.0.0.0";
        private readonly string _driverFilename = Directory.GetApplicationDirectory() + "\\Driver.dll";

        // Driver and panel
        private XpanelForSmartGraphics _panel;
        private IBasicVideoDisplay _device;
    }
}