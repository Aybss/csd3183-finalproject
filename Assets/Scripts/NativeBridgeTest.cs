using UnityEngine;

/// <summary>
/// Quick smoke test for the PathfinderCore native plugin.
/// Attach to any empty GameObject in a test scene and press Play.
/// Check the Console for results.
/// </summary>
public class NativeBridgeTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[Test] Initializing a 10x10 grid...");
        NativeBridge.Init(10, 10);

        Debug.Log("[Test] Blocking a wall across column 5, rows 0-7 (leaving a gap at the top)...");
        for (int y = 0; y < 8; y++)
            NativeBridge.SetBlocked(5, y, true);

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
    }
}
