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
}