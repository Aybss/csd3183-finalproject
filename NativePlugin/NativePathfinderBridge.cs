using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativePathfinderBridge : MonoBehaviour 
{
    // Keeping your designated target DLL name
    const string DLL_NAME = "CooperativeMazeSurvival";

    // --------------------------------------------------------
    // C++ DLL IMPORTS
    // --------------------------------------------------------
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void InitializeGrid(int width, int height);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetWallData(bool[] flatWallArray);

    // NEW: Phase 3 Resource Setters
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetFoodData(int[] flatFoodArray);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetWoodData(int[] flatWoodArray);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetBuildSite(int row, int col);

    // UPDATED: Replaced profileType with agentID to route to specific AgentMemory grids
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RequestPath(int agentID, int startR, int startC, int endR, int endC, int[] outBuffer);

    // NEW: SLAM & Perception Exports
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void AgentPerceive(int agentID, int row, int col, int sightRadius);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void TriggerSLAMSync(int agentA_ID, int agentB_ID);

    // --------------------------------------------------------
    // UNITY HELPER WRAPPERS
    // --------------------------------------------------------

    // Updated to accept the new resource arrays and build site coordinate
    public void SetupBackendMap(int width, int height, bool[] walls, int[] food, int[] wood, Vector2Int buildSite) 
    {
        InitializeGrid(width, height);
        SetWallData(walls);
        
        SetFoodData(food);
        SetWoodData(wood);
        SetBuildSite(buildSite.y, buildSite.x);

        Debug.Log("C++ Backend survival map initialized!");
    }

    // Updated to pass the agentID so C++ uses their personal Fog of War memory
    public Vector2Int[] GetAgentPath(int agentID, Vector2Int start, Vector2Int end) 
    {
        int[] buffer = new int[2000]; // Allocate memory for C++ to write into
        
        // Pass agentID down to the C++ AStarPathfinder wrapper
        int pathLength = RequestPath(agentID, start.y, start.x, end.y, end.x, buffer);

        Vector2Int[] finalPath = new Vector2Int[pathLength];
        for (int i = 0; i < pathLength; i++) 
        {
            // Unpack the flat [row, col, row, col] array back into Vectors
            finalPath[i] = new Vector2Int(buffer[i * 2 + 1], buffer[i * 2]);
        }
        return finalPath;
    }

    // NEW: Standard coordinate conversion helper for Member 5's AI scripts
    public static Vector2Int WorldToGrid(Vector3 worldPos, float cellSize)
    {
        int col = Mathf.FloorToInt(worldPos.x / cellSize);
        int row = Mathf.FloorToInt(worldPos.z / cellSize);
        return new Vector2Int(col, row);
    }
}