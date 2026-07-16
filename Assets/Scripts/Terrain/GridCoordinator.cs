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

    // Fired every time an agent is actually spawned (initial batch or later,
    // e.g. from the simulation UI's Create Agent button) — lets other
    // systems (SimulationGameplayBridge) react without polling.
    public static event System.Action<UnityAgent> OnAgentSpawned;

    private void Start()
    {
        // Always regenerate here rather than relying on PrimsTerrainGenerator's
        // own Start() having already run — Unity doesn't guarantee execution
        // order between the two, so this stays self-sufficient.
        RunSimulationSetup(regenerateMap: true);
    }

    // Full (re)initialization: clears out any existing agents, optionally
    // regenerates the terrain, resyncs the native grid, rebuilds the path
    // network, resets the build site, and spawns a fresh batch of agents.
    // Used both at first Start() and by the simulation UI's Random Map /
    // Restart buttons.
    public void RestartSimulation(bool regenerateMap)
    {
        ClearActiveAgents();
        RunSimulationSetup(regenerateMap);
    }

    // Same full reset, but replaces the map with a previously saved layout
    // instead of generating a new one. Used by the simulation UI's Load Map
    // button.
    public void LoadSimulationFromSchema(ProceduralTerrain.GridSaveSchema schema)
    {
        if (schema == null) return;

        ClearActiveAgents();
        terrainGenerator.ReconstructMapFromSchema(schema);
        RunSimulationSetup(regenerateMap: false);
    }

    private void ClearActiveAgents()
    {
        foreach (UnityAgent agent in activeAgents)
        {
            if (agent != null) Destroy(agent.gameObject);
        }
        activeAgents.Clear();

        if (HouseConstructionSite.Instance != null) HouseConstructionSite.Instance.ResetProgress();
    }

    private void RunSimulationSetup(bool regenerateMap)
    {
        // 1. Generate the map procedurally in C# first
        if (regenerateMap) terrainGenerator.GenerateNewMap();

        // 2. Sync Unity grid layout data directly with the C++ DLL
        SyncProceduralGridWithNative();

        // 3. Build a Prim's-algorithm path network connecting camp, build
        //    site, and resource clusters (lowers their movement weight).
        Vector2Int buildSiteGridPos = GetBuildSiteGridPosition();
        PrimsPathNetworkBuilder.BuildNetwork(terrainGenerator, spawnBaseCoordinates, buildSiteGridPos);

        // 4. Spawn multiple agents staggered across the grid starting area
        SpawnMultipleAgents();
    }

    private Vector2Int GetBuildSiteGridPosition()
    {
        if (HouseConstructionSite.Instance == null) return new Vector2Int(15, 15);

        float size = terrainGenerator.cellSize;
        Transform site = HouseConstructionSite.Instance.transform;
        return new Vector2Int(
            Mathf.RoundToInt(site.position.x / size),
            Mathf.RoundToInt(site.position.z / size));
    }

    public void SyncProceduralGridWithNative()
    {
        int w = terrainGenerator.width;
        int h = terrainGenerator.height;

        NativeBridge.InitializeGrid(w, h);

        SimpleNodeData[] flatGridData = terrainGenerator.ExportContiguousFlatArray();
        NativeBridge.LoadTerrainGrid(flatGridData, w, h);

        // Rubble tiles near stone deposits are impassable for WheelchairBound
        // agents only (native CellType 2) — tag them after the bulk terrain
        // load, which resets every cell's type to free/blocked.
        foreach (Vector2Int rubble in terrainGenerator.RubbleTiles)
        {
            NativeBridge.SetCellType(rubble.x, rubble.y, 2);
        }

        NativeBridge.SetCampPosition(spawnBaseCoordinates.x, spawnBaseCoordinates.y);
        Vector2Int buildSiteGridPos = GetBuildSiteGridPosition();
        NativeBridge.SetBuildSitePosition(buildSiteGridPos.x, buildSiteGridPos.y);

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
            OnAgentSpawned?.Invoke(unityAgent);
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