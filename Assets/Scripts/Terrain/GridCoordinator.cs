using UnityEngine;
using System.Collections.Generic;
using System.Linq;
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

        // Bridge tiles (native CellType 3) — walkable for everyone, but far
        // costlier for WheelchairBound agents to cross (see Agent::FindPath's
        // RoleCellCostMultiplier).
        foreach (Vector2Int bridge in terrainGenerator.BridgeTiles)
        {
            NativeBridge.SetCellType(bridge.x, bridge.y, 3);
        }

        NativeBridge.SetCampPosition(spawnBaseCoordinates.x, spawnBaseCoordinates.y);
        Vector2Int buildSiteGridPos = GetBuildSiteGridPosition();
        NativeBridge.SetBuildSitePosition(buildSiteGridPos.x, buildSiteGridPos.y);

        Debug.Log($"[GridCoordinator] Successfully synced {flatGridData.Length} tiles with PathfinderCore DLL.");
    }

    private void SpawnMultipleAgents()
    {
        var usedPositions = new HashSet<Vector2Int>();

        for (int i = 0; i < totalAgentsToSpawn; i++)
        {
            // Keep the dynamic assignment of physical C++ roles balanced
            // Cycles: 0 = WheelchairBound, 1 = Blind, 2 = Deaf
            int assignedRole = i % 3;
            Vector2Int spawnPos = FindNearestValidSpawnTile(spawnBaseCoordinates, assignedRole, usedPositions);
            usedPositions.Add(spawnPos);
            SpawnAgentInWorld(spawnPos, assignedRole);
        }
    }

    // Expanding-ring search outward from `near` for the closest tile that's
    // walkable, not sitting on a resource prop, and not rubble for
    // WheelchairBound specifically — used both by the initial spawn batch
    // (which also excludes tiles already claimed this batch) and by any
    // one-off spawn (the simulation UI's Create Agent button, agent type
    // reassignment), so no spawn path ever places an agent on water,
    // rubble it can't stand on, or a resource tile.
    public Vector2Int FindNearestValidSpawnTile(Vector2Int near, int role, HashSet<Vector2Int> exclude = null, int maxSearchRadius = 20)
    {
        for (int radius = 0; radius <= maxSearchRadius; radius++)
        {
            for (int xOffset = -radius; xOffset <= radius; xOffset++)
            {
                for (int yOffset = -radius; yOffset <= radius; yOffset++)
                {
                    // Only the outer ring of this radius — smaller radii already covered the interior.
                    if (Mathf.Max(Mathf.Abs(xOffset), Mathf.Abs(yOffset)) != radius) continue;

                    Vector2Int candidate = near + new Vector2Int(xOffset, yOffset);
                    if (exclude != null && exclude.Contains(candidate)) continue;
                    if (IsValidSpawnTile(candidate, role)) return candidate;
                }
            }
        }

        Debug.LogWarning($"[GridCoordinator] No valid empty, walkable spawn tile found within {maxSearchRadius} tiles of {near}; spawning there anyway.");
        return near;
    }

    // A tile is a valid spawn point if it's walkable, not sitting on a
    // resource prop (Wood/Food/Stone), and — for WheelchairBound specifically
    // — not rubble (Agent::FindPath blocks that role there, even though the
    // tile reads as generically walkable to everyone else).
    private bool IsValidSpawnTile(Vector2Int pos, int role)
    {
        if (NativeBridge.IsWalkable(pos.x, pos.y) == 0) return false;

        BiomeType biome = terrainGenerator.GetBiomeAt(pos.x, pos.y);
        if (biome == BiomeType.Wood || biome == BiomeType.Food || biome == BiomeType.Stone) return false;

        bool isWheelchairBound = role == (int)AgentRoleType.WheelchairBound;
        if (isWheelchairBound && terrainGenerator.RubbleTiles.Contains(pos)) return false;

        return true;
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