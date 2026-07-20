using UnityEngine;
using ProceduralTerrain;

// Wires SimulationEvents (fired by SimulationUIController's buttons/sliders)
// to the real gameplay systems. SimulationManager only ever handled two of
// these (speed/debug) against its own placeholder fields, and the
// map/agent-related events had no real listener at all — the dropdown/
// Create/View/Kill buttons were built against a placeholder AgentController/
// AgentManager scaffold that nothing in the actual simulation populated.
//
// Add this to any single GameObject in the scene (e.g. alongside
// GridCoordinator).
public class SimulationGameplayBridge : MonoBehaviour
{
    public GridCoordinator gridCoordinator;

    private float lastNonZeroSpeed = 1f;
    private AgentManager agentManager;

    private void Awake()
    {
        if (gridCoordinator == null) gridCoordinator = FindObjectOfType<GridCoordinator>();

        // SimulationUIController.RefreshDropdown already correctly reads
        // from AgentManager.activeAgents — it just never had a real
        // AgentManager feeding it real agents.
        agentManager = FindObjectOfType<AgentManager>();
        if (agentManager == null) agentManager = gameObject.AddComponent<AgentManager>();
    }

    private void OnEnable()
    {
        SimulationEvents.OnNewRandomMap += HandleNewRandomMap;
        SimulationEvents.OnRestart += HandleRestart;
        SimulationEvents.OnMapAlignmentToggled += HandleMapAlignmentToggled;
        SimulationEvents.OnSpeedChanged += HandleSpeedChanged;
        SimulationEvents.OnPause += HandlePause;
        SimulationEvents.OnDebugToggled += HandleDebugToggled;
        SimulationEvents.OnRequestCreateAgent += HandleCreateAgent;
        SimulationEvents.OnRequestViewAgent += HandleViewAgent;
        SimulationEvents.OnRequestKillAgent += HandleKillAgent;
        SimulationEvents.OnRequestChangeAgentType += HandleChangeAgentType;

        GridCoordinator.OnAgentSpawned += HandleAgentSpawned;
    }

    private void OnDisable()
    {
        SimulationEvents.OnNewRandomMap -= HandleNewRandomMap;
        SimulationEvents.OnRestart -= HandleRestart;
        SimulationEvents.OnMapAlignmentToggled -= HandleMapAlignmentToggled;
        SimulationEvents.OnSpeedChanged -= HandleSpeedChanged;
        SimulationEvents.OnPause -= HandlePause;
        SimulationEvents.OnDebugToggled -= HandleDebugToggled;
        SimulationEvents.OnRequestCreateAgent -= HandleCreateAgent;
        SimulationEvents.OnRequestViewAgent -= HandleViewAgent;
        SimulationEvents.OnRequestKillAgent -= HandleKillAgent;
        SimulationEvents.OnRequestChangeAgentType -= HandleChangeAgentType;

        GridCoordinator.OnAgentSpawned -= HandleAgentSpawned;
    }

    // --- Map operations ---------------------------------------------------

    private void HandleNewRandomMap()
    {
        if (gridCoordinator == null) return;
        agentManager?.ClearAllAgents();
        FindObjectOfType<SlamDiscoveryBeacons>()?.ResetBeacons();
        FindObjectOfType<FoodSoundCue>()?.ResetEmitters();
        gridCoordinator.RestartSimulation(regenerateMap: true);
    }

    private void HandleRestart()
    {
        if (gridCoordinator == null) return;
        agentManager?.ClearAllAgents();
        gridCoordinator.RestartSimulation(regenerateMap: false);
    }

    // "Map Alignment" originally meant a coordinate shift applied only to
    // visuals while pathfinding/agents/camera stayed on the old coordinates
    // — that was the exact bug reverted earlier this session (terrain drawn
    // in the wrong place relative to everything else). Repurposed here as a
    // safe, real feature instead: snap the camera to a fixed top-down view
    // centered on the map, "aligning" the view to it.
    private void HandleMapAlignmentToggled(bool enabled)
    {
        if (gridCoordinator == null || gridCoordinator.terrainGenerator == null) return;

        FreeFlyCamera cam = FindObjectOfType<FreeFlyCamera>();
        if (cam == null) return;

        PrimsTerrainGenerator generator = gridCoordinator.terrainGenerator;
        Vector3 mapCenter = new Vector3(
            generator.width * generator.cellSize * 0.5f,
            0f,
            generator.height * generator.cellSize * 0.5f);

        cam.SetTopDownAligned(enabled, mapCenter);
    }

    // --- Simulation settings ----------------------------------------------

    private void HandleSpeedChanged(float value)
    {
        Time.timeScale = Mathf.Max(0.01f, value);
        lastNonZeroSpeed = Time.timeScale;
    }

    private void HandlePause()
    {
        Time.timeScale = (Time.timeScale > 0f) ? 0f : lastNonZeroSpeed;
    }

    private void HandleDebugToggled(bool enabled)
    {
        SlamDiscoveryBeacons beacons = FindObjectOfType<SlamDiscoveryBeacons>();
        if (beacons != null) beacons.VisualsEnabled = enabled;

        foreach (LineRenderer line in FindObjectsOfType<LineRenderer>())
        {
            if (line.gameObject.name == "SightRing") line.enabled = enabled;
        }
    }

    // --- Agent operations --------------------------------------------------

    private void HandleAgentSpawned(UnityAgent agent)
    {
        if (agentManager == null || agent == null) return;

        AgentController controller = agent.GetComponent<AgentController>();
        if (controller == null) controller = agent.gameObject.AddComponent<AgentController>();
        controller.SetAgentType(ImpairmentVisuals.LabelForRole((AgentRoleType)agent.role));

        agentManager.RegisterAgent(controller);
    }

    private void HandleCreateAgent()
    {
        if (gridCoordinator == null) return;

        int role = agentManager != null ? agentManager.activeAgents.Count % 3 : 0;
        Vector2Int spawnPos = gridCoordinator.spawnBaseCoordinates + new Vector2Int(Random.Range(-2, 3), Random.Range(-2, 3));
        gridCoordinator.SpawnAgentInWorld(spawnPos, role);
    }

    private void HandleViewAgent(int dropdownIndex)
    {
        AgentStats stats = ResolveAgent(dropdownIndex);
        if (stats == null) return;

        FindObjectOfType<AgentOverlayUI>()?.SelectAgent(stats);
    }

    private void HandleKillAgent(int dropdownIndex)
    {
        AgentStats stats = ResolveAgent(dropdownIndex);
        if (stats == null) return;

        stats.ForceKill();
    }

    // Roles are assigned once, natively, at CreateAgent() time — there's no
    // supported way to reassign an existing agent's role in place. Changing
    // type is therefore a destroy-and-respawn at the same tile, as the new
    // role.
    private void HandleChangeAgentType(int dropdownIndex, string newType)
    {
        if (agentManager == null || gridCoordinator == null) return;

        AgentController oldController = ResolveController(dropdownIndex);
        if (oldController == null) return;

        UnityAgent oldAgent = oldController.GetComponent<UnityAgent>();
        if (oldAgent == null) return;

        int newRole = RoleIndexFromTypeName(newType);
        Vector2Int gridPos = new Vector2Int(
            Mathf.RoundToInt(oldAgent.transform.position.x / oldAgent.cellSize),
            Mathf.RoundToInt(oldAgent.transform.position.z / oldAgent.cellSize));

        agentManager.KillAgent(oldController); // removes from list, destroys, refreshes dropdown
        gridCoordinator.SpawnAgentInWorld(gridPos, newRole);
    }

    private int RoleIndexFromTypeName(string typeName)
    {
        switch (typeName)
        {
            case "Blind": return (int)AgentRoleType.Blind;
            case "Deaf": return (int)AgentRoleType.Deaf;
            case "Cripple": return (int)AgentRoleType.WheelchairBound;
            default: return (int)AgentRoleType.WheelchairBound;
        }
    }

    // Dropdown index 0 is always "Select" (none); 1..N map to
    // agentManager.activeAgents[index - 1] — matching
    // SimulationUIController.RefreshDropdown's population order exactly.
    private AgentStats ResolveAgent(int dropdownIndex)
    {
        AgentController controller = ResolveController(dropdownIndex);
        return controller != null ? controller.GetComponent<AgentStats>() : null;
    }

    private AgentController ResolveController(int dropdownIndex)
    {
        if (agentManager == null) return null;

        int listIndex = dropdownIndex - 1;
        if (listIndex < 0 || listIndex >= agentManager.activeAgents.Count) return null;

        return agentManager.activeAgents[listIndex];
    }
}
