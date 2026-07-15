using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentController : MonoBehaviour
{
    public string agentType;

    public void SetAgentType(string newType)
    {
        agentType = newType;
        Debug.Log($"Agent {name} is now a {agentType}");
    }
}
