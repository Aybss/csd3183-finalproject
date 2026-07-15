using UnityEngine;
using System.Collections.Generic;
using System;

public class AgentManager : MonoBehaviour
{
    // A list of currently alive agents
    public List<AgentController> activeAgents = new List<AgentController>();

    // This event tells the UI: "The list of agents changed, refresh your dropdown!"
    public static event System.Action OnAgentListChanged;

    public void RegisterAgent(AgentController agent) 
    {
        activeAgents.Add(agent);
        OnAgentListChanged?.Invoke();
    }

    public void KillAgent(AgentController agent)
    {
        activeAgents.Remove(agent);
        Destroy(agent.gameObject);
        OnAgentListChanged?.Invoke();
    }

    // For a simulation restart: the agent GameObjects are being destroyed
    // elsewhere (GridCoordinator owns that), this just clears the
    // bookkeeping list and tells the dropdown to refresh. A raw
    // activeAgents.Clear() from outside this class wouldn't raise
    // OnAgentListChanged — C# only allows a class to invoke its own events.
    public void ClearAllAgents()
    {
        activeAgents.Clear();
        OnAgentListChanged?.Invoke();
    }

    private void OnEnable() => SimulationEvents.OnRequestChangeAgentType += ModifyAgent;
    private void OnDisable() => SimulationEvents.OnRequestChangeAgentType -= ModifyAgent;

    private void ModifyAgent(int agentIndex, string newType)
    {
        // 1. Safety check
        if (agentIndex >= 0 && agentIndex < activeAgents.Count)
        {
            // 2. Access the agent and change it
            activeAgents[agentIndex].SetAgentType(newType);
        }
    }
}