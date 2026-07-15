using UnityEngine;
using UnityEngine.UI;
using TMPro;    // TMP_Dropdown

public class SimulationEvent : MonoBehaviour
{
    [SerializeField] private Slider SimulationSpeedSlider;
    [SerializeField] private Toggle DebugDrawingToggle;
    [SerializeField] private Toggle MapAlignmentToggle;
    [SerializeField] private TMP_Dropdown AgentSelectDropdown;

    private void Start()
    {
        // Change simulation speed
        SimulationSpeedSlider.onValueChanged.AddListener(value => {
            SimulationEvents.OnSpeedChanged?.Invoke(value);
        });

        // Toggle debug drawing
        DebugDrawingToggle.onValueChanged.AddListener(state => {
            SimulationEvents.OnDebugToggled?.Invoke(state);
        });

        // Toggle agent map alignment
        MapAlignmentToggle.onValueChanged.AddListener(state => {
            SimulationEvents.OnMapAlignmentToggled?.Invoke(state);
        });

        // Select active agent
        AgentSelectDropdown.onValueChanged.AddListener(index => {
            SimulationEvents.OnAgentSelected?.Invoke(index);
        });
    }
}
