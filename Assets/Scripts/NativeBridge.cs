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
    public static extern void SetCellType(int x, int y, int cellType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetCellWeight(int x, int y, float weight);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsWalkable(int x, int y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsWalkableForAgent(int agentHandle, int x, int y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetBuildSitePosition(int x, int y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetCampPosition(int x, int y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CreateAgent(int role);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void DestroyAgent(int agentHandle);

    // SLAM: clears fog-of-war around (x,y) for this agent (sight + hearing).
    // Call every time the agent's grid position changes.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void AgentPerceive(int agentHandle, int x, int y);

    // SLAM: merges both agents' explored-tile memory when they meet.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void TriggerSLAMSync(int agentHandleA, int agentHandleB);

    // SLAM visualization: has this specific agent's own memory discovered
    // this resource tile yet? biomeType 2=Wood, 3=Food, 4=Stone.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsResourceDiscoveredByAgent(int agentHandle, int biomeType, int x, int y);

    // Bulk fog-of-war query: one call returns an agent's whole exploredTiles
    // bitmap (row-major, index = y*width+x) instead of one call per tile.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetExploredTiles(int agentHandle, byte[] outBuffer, int bufferSize);

    public static bool[] GetExploredTiles(int agentHandle, int width, int height)
    {
        int total = width * height;
        byte[] buffer = new byte[total];
        int count = GetExploredTiles(agentHandle, buffer, total);

        bool[] result = new bool[total];
        for (int i = 0; i < count; i++)
        {
            result[i] = buffer[i] != 0;
        }
        return result;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void AddSoundCue(float x, float y, float radius, float costPenalty);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearSoundCues();

    // Group resource coordination: biomeType 2=Wood, 3=Food, 4=Stone.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetAvailableResource(int agentHandle, int biomeType, int agentX, int agentY, out int outX, out int outY);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ReleaseResource(int biomeType, int x, int y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearResourceTile(int biomeType, int x, int y);

    // GOAP: push the agent's current survival stats into its native Blackboard.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SyncAgentBlackboard(int agentHandle, float hunger, float thirst, float fatigue,
        int woodCarried, int maxWood, int stoneCarried, int maxStone);

    // GOAP: ask the native planner what this agent should do next. Returns an
    // AgentAction code (see Assets/Scripts/Agent/GOAP/AgentAction.cs); for
    // Navigate* actions, outTargetX/Y is also populated with a reserved tile.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int PlanNextAction(int agentHandle, int agentX, int agentY, out int outTargetX, out int outTargetY);

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

    // Prim's algorithm: builds a minimum-spanning-tree path network over the
    // given points of interest. Returns edges as index pairs into (xs, ys).
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int GeneratePathNetwork(int[] xs, int[] ys, int count, int[] outFromIdx, int[] outToIdx, int maxEdges);

    public static List<(int fromIndex, int toIndex)> GeneratePathNetwork(List<Vector2Int> points)
    {
        var edges = new List<(int, int)>();
        if (points == null || points.Count < 2) return edges;

        int[] xs = new int[points.Count];
        int[] ys = new int[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            xs[i] = points[i].x;
            ys[i] = points[i].y;
        }

        int maxEdges = points.Count;
        int[] outFrom = new int[maxEdges];
        int[] outTo = new int[maxEdges];

        int count = GeneratePathNetwork(xs, ys, points.Count, outFrom, outTo, maxEdges);
        for (int i = 0; i < count; i++)
        {
            edges.Add((outFrom[i], outTo[i]));
        }
        return edges;
    }
}
