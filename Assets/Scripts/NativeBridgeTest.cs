using UnityEngine;

public class NativeBridgeTest : MonoBehaviour
{
    private void Start()
    {
        RunNativeTest();
    }

    public void RunNativeTest()
    {
        int width = 10;
        int height = 10;

        Debug.Log("[Test] Initializing native grid...");
        NativeBridge.InitializeGrid(width, height);

        NativeBridge.SetCellBlocked(2, 2, 1);
        NativeBridge.SetCellBlocked(2, 3, 1);
        NativeBridge.SetCellBlocked(2, 4, 1);

        int[] pathX = new int[100];
        int[] pathY = new int[100];

        Debug.Log("[Test] Requesting path from (1,1) to (5,5)...");
        int pathLength = NativeBridge.FindPath(1, 1, 5, 5, pathX, pathY, pathX.Length);

        if (pathLength > 0)
        {
            Debug.Log($"[Test] Path found! Length: {pathLength} steps.");
            for (int i = 0; i < pathLength; i++)
            {
                Debug.Log($"Step {i + 1}: ({pathX[i]}, {pathY[i]})");
            }
        }
        else if (pathLength == -1)
        {
            Debug.LogWarning("[Test] No path exists between those points.");
        }
        else if (pathLength == -2)
        {
            Debug.LogError("[Test] Path found, but it is longer than the pre-allocated buffer size.");
        }
    }
}