using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// P/Invoke bridge to the PathfinderCore native plugin (C++ DLL).
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

    // --- Per-agent perception ("disabilities") ---

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CreateAgent(int visionRange, int canHear);

    [DllImport(DLL_NAME, EntryPoint = "DestroyAgent", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DestroyAgentNative(int agentHandle);

    [DllImport(DLL_NAME, EntryPoint = "UpdateAgentVision", CallingConvention = CallingConvention.Cdecl)]
    private static extern void UpdateAgentVisionNative(int agentHandle, int x, int y);

    [DllImport(DLL_NAME, EntryPoint = "AddSoundCue", CallingConvention = CallingConvention.Cdecl)]
    private static extern void AddSoundCueNative(float x, float y, float radius, float costPenalty);

    [DllImport(DLL_NAME, EntryPoint = "ClearSoundCues", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ClearSoundCuesNative();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY,
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

    /// <summary>Loads a full occupancy grid at once.</summary>
    public static void LoadObstacles(byte[] occupancyData)
    {
        LoadObstacleData(occupancyData, occupancyData.Length);
    }

    /// <summary>Finds a path between two grid cells.</summary>
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

    /// <summary>Creates a new agent with its own sensory traits.</summary>
    public static int CreateAgent(int visionRange, bool canHear)
    {
        return CreateAgent(visionRange, canHear ? 1 : 0);
    }

    /// <summary>Destroys an agent by its handle and frees up memory.</summary>
    public static void DestroyAgent(int agentHandle)
    {
        if (agentHandle < 0) return;
        DestroyAgentNative(agentHandle);
    }

    /// <summary>Reveals the world around (x, y) to the given agent.</summary>
    public static void UpdateAgentVision(int agentHandle, int x, int y)
    {
        if (agentHandle < 0) return;
        UpdateAgentVisionNative(agentHandle, x, y);
    }

    /// <summary>Registers a sound event in the world.</summary>
    public static void AddSoundCue(float x, float y, float radius, float costPenalty)
    {
        AddSoundCueNative(x, y, radius, costPenalty);
    }

    /// <summary>Clears all currently-registered sound cues.</summary>
    public static void ClearSoundCues()
    {
        ClearSoundCuesNative();
    }

    /// <summary>Plans a path using the given agent's own knowledge.</summary>
    public static List<Vector2Int> FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY,
                                                   int maxPathLength = 4096)
    {
        if (agentHandle < 0) return null;

        int[] outX = new int[maxPathLength];
        int[] outY = new int[maxPathLength];

        int result = FindAgentPath(agentHandle, startX, startY, endX, endY, outX, outY, maxPathLength);

        if (result < 0)
        {
            if (result == -2)
                Debug.LogWarning("[NativeBridge] Agent path found but exceeded maxPathLength — increase the buffer size.");
            return null;
        }

        var path = new List<Vector2Int>(result);
        for (int i = 0; i < result; i++)
            path.Add(new Vector2Int(outX[i], outY[i]));

        return path;
    }
}