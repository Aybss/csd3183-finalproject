using UnityEngine;

public class NativeBridgeTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[Test] Initializing a 10x10 grid...");
        NativeBridge.Init(10, 10);

        Debug.Log("[Test] Blocking a wall across column 5, rows 0-7 (leaving a gap at the top)...");
        for (int y = 0; y < 8; y++)
            NativeBridge.SetBlocked(5, y, 1);

        Debug.Log("[Test] Finding path from (0,0) to (9,9)...");
        var path = NativeBridge.FindPath(0, 0, 9, 9);

        if (path == null)
        {
            Debug.LogError("[Test] FAILED — no path found. Something is wrong with the grid or the wall fully blocks it.");
            return;
        }

        Debug.Log($"[Test] SUCCESS — path found with {path.Count} steps:");
        string pathStr = "";
        foreach (var cell in path)
            pathStr += $"({cell.x},{cell.y}) ";
        Debug.Log(pathStr);

        TestBlindAgent();
        TestDeafAgent();
    }

    private void TestBlindAgent()
    {
        Debug.Log("[Test] Creating a blind agent (role = 1) at (0,0)...");
        int blindHandle = NativeBridge.CreateAgent(1);
        NativeBridge.UpdateAgentVision(blindHandle, 0, 0);

        Debug.Log("[Test] Blind agent plans toward (9,0) without having seen the wall yet...");
        var blindPlan = NativeBridge.FindAgentPath(blindHandle, 0, 0, 9, 0);

        if (blindPlan == null)
        {
            Debug.LogError("[Test] FAILED — blind agent found no initial plan.");
            return;
        }
        Debug.Log($"[Test] Initial (optimistic) plan has {blindPlan.Count} steps — it doesn't know about the wall yet.");

        // Walk it forward, updating vision each step, until it "sees" the wall.
        foreach (var cell in blindPlan)
        {
            NativeBridge.UpdateAgentVision(blindHandle, cell.x, cell.y);
            if (Mathf.Abs(cell.x - 5) <= 1)
            {
                Debug.Log($"[Test] Blind agent at ({cell.x},{cell.y}) now perceives the wall. Replanning...");
                var replanned = NativeBridge.FindAgentPath(blindHandle, cell.x, cell.y, 9, 0);
                Debug.Log(replanned == null
                    ? "[Test] FAILED — no path after replanning."
                    : $"[Test] SUCCESS — replanned path has {replanned.Count} steps.");
                return;
            }
        }
    }

    private void TestDeafAgent()
    {
        Debug.Log("[Test] Adding a loud sound cue at (7,5)...");
        NativeBridge.AddSoundCue(7f, 5f, 2f, 50f);

        int hearingHandle = NativeBridge.CreateAgent(0);
        int deafHandle = NativeBridge.CreateAgent(2);

        var hearingPath = NativeBridge.FindAgentPath(hearingHandle, 0, 5, 9, 5);
        var deafPath = NativeBridge.FindAgentPath(deafHandle, 0, 5, 9, 5);

        Debug.Log(hearingPath == null
            ? "[Test] FAILED — hearing agent found no path."
            : $"[Test] Hearing agent path has {hearingPath.Count} steps (should detour around the sound).");
        Debug.Log(deafPath == null
            ? "[Test] FAILED — deaf agent found no path."
            : $"[Test] Deaf agent path has {deafPath.Count} steps (should ignore the sound and go straight through).");

        NativeBridge.ClearSoundCues();
    }
}