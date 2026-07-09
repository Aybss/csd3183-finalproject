using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// P/Invoke bridge to the PathfinderCore native plugin (C++ DLL).
///
/// SETUP: after building PathfinderCore, copy the resulting
/// PathfinderCore.dll into Assets/Plugins/PathfinderCore.dll
/// (create the Plugins folder if it doesn't exist — Unity looks
/// there specifically for native plugins).
/// </summary>
public static class NativeBridge
{
    private const string DLL_NAME = "PathfinderCore";

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int InitializeGrid(int width, int height);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetCellBlocked(int x, int y, int blocked);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void LoadObstacleData(byte[] data, int length);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FindPath(int startX, int startY, int endX, int endY,
                                        int[] outX, int[] outY, int maxPathLength);

    /// <summary>Call once at startup, matching your ObstacleGrid's dimensions.</summary>
    public static void Init(int width, int height)
    {
        InitializeGrid(width, height);
    }

    /// <summary>Marks a single cell blocked/free in the native grid.</summary>
    public static void SetBlocked(int x, int y, bool blocked)
    {
        SetCellBlocked(x, y, blocked ? 1 : 0);
    }

    /// <summary>
    /// Loads a full occupancy grid at once — pass the result of
    /// ObstacleGrid.ExportOccupancyBytes() directly.
    /// </summary>
    public static void LoadObstacles(byte[] occupancyData)
    {
        LoadObstacleData(occupancyData, occupancyData.Length);
    }

    /// <summary>
    /// Finds a path between two grid cells. Returns null if no path
    /// exists, otherwise a list of grid cells from start to end.
    /// </summary>
    public static List<Vector2Int> FindPath(int startX, int startY, int endX, int endY, int maxPathLength = 4096)
    {
        int[] outX = new int[maxPathLength];
        int[] outY = new int[maxPathLength];

        int result = FindPath(startX, startY, endX, endY, outX, outY, maxPathLength);

        if (result < 0)
        {
            if (result == -2)
                Debug.LogWarning("[NativeBridge] Path found but exceeded maxPathLength — increase the buffer size.");
            return null;
        }

        var path = new List<Vector2Int>(result);
        for (int i = 0; i < result; i++)
            path.Add(new Vector2Int(outX[i], outY[i]));

        return path;
    }
}
