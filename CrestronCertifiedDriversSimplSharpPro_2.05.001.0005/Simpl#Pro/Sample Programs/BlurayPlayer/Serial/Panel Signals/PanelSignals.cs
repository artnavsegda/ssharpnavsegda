namespace RadBlurayPlayerSerialExample
{
    public enum PanelDigitalSignals
    {
        PowerOn = 31,
        PowerOff = 32,
        PowerToggle = 33,
        ReverseSkip = 40,
        ReverseScan = 41,
        Stop = 42,
        Pause = 43,
        Play = 44,
        ForwardScan = 45,
        ForwardSkip = 46,
        Audio = 50,
        Display = 51,
        Eject = 52,
        Subtitle = 53,
        Options = 54,
    }

    public enum PanelSerialSignals
    {
        PowerFeedback = 3,
        ConnectionFeedback = 1,
        ManufacturerFeedback = 80,
        ModelFeedback = 81
    }

    public enum PanelSmartObjects
    {
        DPad = 1
    }
}