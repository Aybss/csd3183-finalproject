using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    public float simulationSpeed = 1.0f;
    public bool debugDrawingEnabled = false;

    private void OnEnable()
    {
        // Start listening to the events
        SimulationEvents.OnSpeedChanged += HandleSpeedChanged;
        SimulationEvents.OnDebugToggled += HandleDebugToggled;
    }

    private void OnDisable()
    {
        // Always unsubscribe to avoid memory leaks if the manager is destroyed
        SimulationEvents.OnSpeedChanged -= HandleSpeedChanged;
        SimulationEvents.OnDebugToggled -= HandleDebugToggled;
    }

    private void HandleSpeedChanged(float newSpeed)
    {
        simulationSpeed = newSpeed;
        Debug.Log($"Simulation speed updated to: {simulationSpeed}");
    }

    private void HandleDebugToggled(bool isEnabled)
    {
        debugDrawingEnabled = isEnabled;
        Debug.Log($"Debug drawing status: {debugDrawingEnabled}");
    }
}
