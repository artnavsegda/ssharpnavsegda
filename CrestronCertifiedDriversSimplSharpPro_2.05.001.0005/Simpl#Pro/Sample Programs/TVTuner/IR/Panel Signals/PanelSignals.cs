namespace RadTvTunerIRExample
{
    public enum PanelDigitalSignals
    {
        PowerOn = 31,
        PowerOff = 32,
        PowerToggle = 33,
        ChanPlus = 34,
        ChanMinus = 35,
        VolPlus = 36,
        VolMinus = 37,
        ReverseSkip = 40,
        ReverseScan = 41,
        Stop = 42,
        Pause = 43,
        Play = 44,
        ForwardScan = 45,
        ForwardSkip = 46,
        Key0 = 100,
        Key1 = 101,
        Key2 = 102,
        Key3 = 103,
        Key4 = 104,
        Key5 = 105,
        Key6 = 106,
        Key7 = 107,
        Key8 = 108,
        Key9 = 109,
        KeyDash = 110,
        KeyEnter = 111
    }

    public enum PanelSerialSignals
    {
        ManufacturerFeedback = 80,
        ModelFeedback = 81
    }

    public enum PanelSmartObjects
    {
        DPad = 1
    }
}