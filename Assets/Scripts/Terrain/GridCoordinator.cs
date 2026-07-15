using UnityEngine;
using System.Collections.Generic;
using ProceduralTerrain;

public class GridCoordinator : MonoBehaviour
{
    [Header("References")]
    public PrimsTerrainGenerator terrainGenerator;

    [Header("Agent Spawning Configuration")]
    public GameObject agentPrefab;
    public Vector2Int spawnBaseCoordinates = new Vector2Int(2, 2);
    public float spawnYOffset = 1.0f;

    [Header("Multi-Agent Settings")]
    [Range(1, 20)] public int totalAgentsToSpawn = 10; // <--- Set this to 10 in the Inspector!

    private List<UnityAgent> activeAgents = new List<UnityAgent>();

    private void Start()
    {
        // 1. Generate the map procedurally in C# first
        terrainGenerator.GenerateNewMap();

        // 2. Sync Unity grid layout data directly with the C++ DLL
        SyncProceduralGridWithNative();

        // 3. Spawn multiple agents staggered across the grid starting area
        SpawnMultipleAgents();
    }

    public void SyncProceduralGridWithNative()
    {
        int w = terrainGenerator.width;
        int h = terrainGenerator.height;

        NativeBridge.InitializeGrid(w, h);

        SimpleNodeData[] flatGridData = terrainGenerator.ExportContiguousFlatArray();
        NativeBridge.LoadTerrainGrid(flatGridData, w, h);

        Debug.Log($"[GridCoordinator] Successfully synced {flatGridData.Length} tiles with PathfinderCore DLL.");
    }

    private void SpawnMultipleAgents()
    {
        int spawnedCount = 0;
        int maxAttempts = totalAgentsToSpawn * 5; // Guard rails to prevent infinite loops

        // Try to find unique nearby grid spaces to prevent physics collision bugs
        for (int xOffset = 0; xOffset < 5 && spawnedCount < totalAgentsToSpawn; xOffset++)
        {
            for (int yOffset = 0; yOffset < 5 && spawnedCount < totalAgentsToSpawn; yOffset++)
            {
                Vector2Int spawnPos = new Vector2Int(spawnBaseCoordinates.x + xOffset, spawnBaseCoordinates.y + yOffset);

                // Keep the dynamic assignment of physical C++ roles balanced
                // Cycles: 0 = WheelchairBound, 1 = Blind, 2 = Deaf
                int assignedRole = spawnedCount % 3;

                SpawnAgentInWorld(spawnPos, assignedRole);
                spawnedCount++;
            }
        }
    }

    public void SpawnAgentInWorld(Vector2Int gridPos, int agentRole)
    {
        // 1. Register the agent inside the C++ DLL and get its unique pointer handle
        int nativeHandle = NativeBridge.CreateAgent(agentRole);

        if (nativeHandle < 0)
        {
            Debug.LogError("[GridCoordinator] Failed to allocate Agent in C++!");
            return;
        }

        // 2. Calculate the corresponding world position, applying the custom Y offset
        float size = terrainGenerator.cellSize;
        Vector3 spawnWorldPos = new Vector3(gridPos.x * size, spawnYOffset, gridPos.y * size);

        // 3. Instantiate physical GameObject inside Unity
        GameObject agentObj = Instantiate(agentPrefab, spawnWorldPos, Quaternion.identity);

        // 4. Configure the UnityAgent script parameters
        UnityAgent unityAgent = agentObj.GetComponent<UnityAgent>();
        if (unityAgent != null)
        {
            unityAgent.Initialize(nativeHandle, agentRole, size);
            activeAgents.Add(unityAgent);
            Debug.Log($"[GridCoordinator] Spawned Agent {activeAgents.Count} (Role: {agentRole}) at grid ({gridPos.x}, {gridPos.y}) with C++ Handle: {nativeHandle}");
        }
        else
        {
            Debug.LogError("[GridCoordinator] Agent Prefab is missing the UnityAgent component!");
        }
    }

    // Call this to issue commands to all active agents (e.g., on a mouse click)
    public void OrderAgentsToTarget(Vector2Int targetCoordinates)
    {
        foreach (var agent in activeAgents)
        {
            if (agent != null)
            {
                int currentX = Mathf.RoundToInt(agent.transform.position.x / terrainGenerator.cellSize);
                int currentY = Mathf.RoundToInt(agent.transform.position.z / terrainGenerator.cellSize);

                agent.SetNewDestination(new Vector2Int(currentX, currentY), targetCoordinates);
            }
        }
    }
}