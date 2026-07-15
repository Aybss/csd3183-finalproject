using System;

public static class SimulationEvents
{
    // Simulation settings
    public static Action<float> OnSpeedChanged;
    public static Action<bool>  OnDebugToggled;
    public static Action<bool>  OnMapAlignmentToggled;
    public static Action        OnPause;
    public static Action        OnRestart;

    // Agent operations
    public static Action<int>           OnAgentSelected;
    public static Action<int, string>   OnRequestChangeAgentType;

    // Map operations
    public static Action    OnNewRandomMap;
    public static Action    OnLoadMap;
    public static Action    OnOpenMapEditorButton;
}
