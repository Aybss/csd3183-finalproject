using System;

public static class SimulationEvents
{
    public static Action<float>     OnSpeedChanged;
    public static Action<bool>      OnDebugToggled;
    public static Action<bool>      OnMapAlignmentToggled;
    public static Action<int>       OnAgentSelected;
    public static Action<int, string>       OnRequestChangeAgentType;
}
