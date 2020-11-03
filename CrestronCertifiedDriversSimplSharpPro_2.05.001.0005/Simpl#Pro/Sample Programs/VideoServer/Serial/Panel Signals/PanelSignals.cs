namespace RadVideoServerSerialExample
{
    public enum PanelDigitalSignals
    {
        ReverseSkip = 40,
        ReverseScan = 41,
        Stop = 42,
        Pause = 43,
        Play = 44,
        ForwardScan = 45,
        ForwardSkip = 46,
    }

    public enum PanelSerialSignals
    {
        ConnectionFeedback = 1,
        ManufacturerFeedback = 80,
        ModelFeedback = 81
    }

    public enum PanelSmartObjects
    {
        DPad = 1
    }
}