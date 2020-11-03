namespace RadVideoServerCecExample
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
        Home = 47,
        CustomCommand1 = 48,
        Exit = 49
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