using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace RadDisplayEthernetExample
{
    public enum PanelDigitalSignals
    {
        PowerOn = 31,
        PowerOff = 32,
        PowerToggle = 33,
        MuteOn = 34,
        MuteOff = 35,
        MuteToggle = 36,
        VolPlus = 40,
        VolMinus = 41,
        Connect = 50,
        Disconnect = 51,
    }

    public enum PanelSerialSignals
    {
        InputFeedback = 70,
        PowerFeedback = 71,
        MuteFeedback = 72,
        ConnectionFeedback = 74,
        ManufacturerFeedback = 80,
        ModelFeedback = 81
    }

    public enum PanelAnalogSignals
    {
        VolumeValue = 10
    }

    public enum PanelSmartObjects
    {
        DisplayInputList = 1
    }
}