namespace RadDisplayIrExample
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
        ManufacturerFeedback = 80,
        ModelFeedback = 81
    }

    public enum PanelSmartObjects
    {
        DisplayInputList = 1
    }
}