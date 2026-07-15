using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using ProceduralTerrain;

public static class NativeBridge
{
    private const string DllName = "PathfinderCore";

    [DllImport(DllName, EntryPoint = "InitializeGrid", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Init(int width, int height);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int InitializeGrid(int width, int height);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void LoadTerrainGrid(SimpleNodeData[] gridData, int width, int height);

    [DllImport(DllName, EntryPoint = "SetCellBlocked", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetBlocked(int x, int y, int blocked);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetCellBlocked(int x, int y, int blocked);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CreateAgent(int role);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void DestroyAgent(int agentHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void UpdateAgentVision(int agentHandle, int x, int y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void AddSoundCue(float x, float y, float radius, float costPenalty);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearSoundCues();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FindPath(int startX, int startY, int endX, int endY, int[] outX, int[] outY, int maxPathLength);

    public static List<Vector2Int> FindPath(int startX, int startY, int endX, int endY)
    {
        int maxPathLength = 1024;
        int[] outX = new int[maxPathLength];
        int[] outY = new int[maxPathLength];

        int length = FindPath(startX, startY, endX, endY, outX, outY, maxPathLength);

        if (length <= 0) return new List<Vector2Int>();

        List<Vector2Int> path = new List<Vector2Int>(length);
        for (int i = 0; i < length; i++)
        {
            path.Add(new Vector2Int(outX[i], outY[i]));
        }
        return path;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY, int[] outX, int[] outY, int maxPathLength);

    public static List<Vector2Int> FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY)
    {
        int maxPathLength = 1024;
        int[] outX = new int[maxPathLength];
        int[] outY = new int[maxPathLength];

        int length = FindAgentPath(agentHandle, startX, startY, endX, endY, outX, outY, maxPathLength);

        if (length <= 0) return new List<Vector2Int>();

        List<Vector2Int> path = new List<Vector2Int>(length);
        for (int i = 0; i < length; i++)
        {
            path.Add(new Vector2Int(outX[i], outY[i]));
        }
        return path;
    }

    public static List<Vector2Int> FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY, int ignoredParam)
    {
        return FindAgentPath(agentHandle, startX, startY, endX, endY);
    }

    public static List<Vector2Int> GetDiscoveredResources(int resourceType, int width, int height)
    {
        List<Vector2Int> resources = new List<Vector2Int>();
        var generator = Object.FindObjectOfType<PrimsTerrainGenerator>();

        if (generator == null) return resources;

        var grid = generator.ExportContiguousFlatArray();
        for (int i = 0; i < grid.Length; i++)
        {
            if (grid[i].biomeType == resourceType)
            {
                resources.Add(new Vector2Int(grid[i].x, grid[i].y));
            }
        }
        return resources;
    }
}