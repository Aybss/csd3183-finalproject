using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;    // TMP_Dropdown

public class SimulationEvent : MonoBehaviour
{
    // General Simulation Settings
    [SerializeField] private Slider SimulationSpeedSlider;
    [SerializeField] private Toggle DebugDrawingToggle;
    [SerializeField] private Toggle MapAlignmentToggle;

    // Agent Interactions
    [SerializeField] private TMP_Dropdown AgentSelectDropdown;
    [SerializeField] private Button CreateAgentButton;
    [SerializeField] private Button ViewAgentButton;
    [SerializeField] private TMP_Dropdown AgentTypeDropdown;
    [SerializeField] private Button KillAgentButton;

    private int CurrentAgentIndex = 0;

    private void Start()
    {
        // Agent Interactions --------------------------------------------------------//
        // ---------------------------------------------------------------------------//

        // Initialize dropdowns
        AgentTypeDropdown.ClearOptions();
        AgentTypeDropdown.AddOptions(new List<string> { "Blind", "Deaf", "Cripple" });
        AgentSelectDropdown.ClearOptions();
        AgentTypeDropdown.AddOptions(new List<string> { "Select" });

        // Agent buttons initial state: Greyed out
        SetAgentButtonsInteractable(false);    
        OnAgentSelected(0);

        // Listen to AgentList changes
        AgentManager.OnAgentListChanged += RefreshDropdown;
        AgentSelectDropdown.onValueChanged.AddListener(OnAgentSelected);

        // Broadcast simulation events -----------------------------------------------//
        // ---------------------------------------------------------------------------//

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
    }

    private void RefreshDropdown()
    {
        var manager = Object.FindAnyObjectByType<AgentManager>();
        AgentSelectDropdown.ClearOptions();

        // Add "Select" as index 0 (Default)
        var options = new List<string> { "Select" };
        foreach(var agent in manager.activeAgents)
            options.Add(agent.name);

        AgentSelectDropdown.AddOptions(options);

    }

    private void OnAgentTypeChanged(int index)
    {
        string selectedType = AgentTypeDropdown.options[index].text;
        SimulationEvents.OnRequestChangeAgentType?.Invoke(CurrentAgentIndex, selectedType);
    }

    private void OnAgentSelected(int index)
    {
        // If index is 0, treat as "No Agent" selected
        CurrentAgentIndex = index;
        bool isAgentSelected = index > 0;
        SetAgentButtonsInteractable(isAgentSelected);
    }

    private void SetAgentButtonsInteractable(bool state)
    {
        KillAgentButton.interactable = state;
        ViewAgentButton.interactable = state;
        AgentTypeDropdown.interactable = state;
    }
}
