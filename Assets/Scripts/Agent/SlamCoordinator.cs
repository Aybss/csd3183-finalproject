using UnityEngine;

// The "group" half of SLAM: when two agents get close enough to notice each
// other, they merge what they've each explored (AgentMemory::Merge in
// PluginMain.cpp/TriggerSLAMSync). A blind agent that's never seen the wood
// cluster can still be routed there once a deaf teammate who has walks past.
// Attach this to any single GameObject in the gameplay scene (e.g. the same
// object that holds GridCoordinator).
public class SlamCoordinator : MonoBehaviour
{
    [Tooltip("World-space distance within which two agents share their explored map.")]
    public float communicationRadius = 4f;
    [Tooltip("How often to check agent pairs, in seconds. Doesn't need to be every frame.")]
    public float checkInterval = 0.5f;

    private float timer = 0f;

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        UnityAgent[] agents = FindObjectsOfType<UnityAgent>();
        float radiusSqr = communicationRadius * communicationRadius;

        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i].agentHandle < 0) continue;

            for (int j = i + 1; j < agents.Length; j++)
            {
                if (agents[j].agentHandle < 0) continue;

                float distSqr = (agents[i].transform.position - agents[j].transform.position).sqrMagnitude;
                if (distSqr <= radiusSqr)
                {
                    NativeBridge.TriggerSLAMSync(agents[i].agentHandle, agents[j].agentHandle);
                }
            }
        }
    }
}
