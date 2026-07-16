using System.Collections.Generic;
using UnityEngine;
using ProceduralTerrain;

// Visualizes SLAM's fog-of-war directly: a small glowing beacon hovers over
// every wood/food/stone tile, but only lights up once the agent currently
// selected in AgentOverlayUI has actually discovered that specific tile in
// its own memory (NativeBridge.IsResourceDiscoveredByAgent). Select
// different agents in the overlay and watch which beacons turn on/off —
// each agent's own, different, partial knowledge of the map, made visible.
public class SlamDiscoveryBeacons : MonoBehaviour
{
    [Tooltip("How often to re-check discovery state against the selected agent, in seconds.")]
    public float refreshInterval = 0.3f;

    private class Beacon
    {
        public Vector2Int tile;
        public int biomeType;
        public GameObject marker;
    }

    private readonly List<Beacon> beacons = new List<Beacon>();
    private bool initialized;
    private float refreshTimer;
    // Also toggles overall visibility from the simulation UI's "Debug
    // Drawing" checkbox (see SimulationGameplayBridge) — separate from the
    // per-beacon discovered/undiscovered state.
    public bool VisualsEnabled = true;

    // Called after a map regenerates — old beacons point at resource tiles
    // that may no longer exist (or exist somewhere else entirely), and the
    // native memory they were reading from was just wiped by re-init.
    public void ResetBeacons()
    {
        foreach (Beacon beacon in beacons)
        {
            if (beacon.marker != null) Destroy(beacon.marker);
        }
        beacons.Clear();
        initialized = false;
    }

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
            return;
        }

        foreach (Beacon beacon in beacons)
        {
            if (beacon.marker.activeSelf) beacon.marker.transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = refreshInterval;

        RefreshVisibility();
    }

    // Terrain generation can finish after this component's own Start(), so
    // keep retrying each Update until resource tiles actually exist.
    private void TryInitialize()
    {
        PrimsTerrainGenerator generator = FindObjectOfType<PrimsTerrainGenerator>();
        if (generator == null) return;

        List<Vector2Int> woodTiles = generator.FindResourceTiles(BiomeType.Wood);
        List<Vector2Int> foodTiles = generator.FindResourceTiles(BiomeType.Food);
        List<Vector2Int> stoneTiles = generator.FindResourceTiles(BiomeType.Stone);

        if (woodTiles.Count == 0 && foodTiles.Count == 0 && stoneTiles.Count == 0) return;

        SpawnBeacons(woodTiles, (int)BiomeType.Wood, new Color(0.4f, 0.9f, 0.3f), generator.cellSize);
        SpawnBeacons(foodTiles, (int)BiomeType.Food, new Color(0.95f, 0.85f, 0.2f), generator.cellSize);
        SpawnBeacons(stoneTiles, (int)BiomeType.Stone, new Color(0.8f, 0.8f, 0.85f), generator.cellSize);

        initialized = true;
    }

    private void SpawnBeacons(List<Vector2Int> tiles, int biomeType, Color color, float cellSize)
    {
        foreach (Vector2Int tile in tiles)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"SlamBeacon_{biomeType}_{tile.x}_{tile.y}";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = new Vector3(tile.x * cellSize, 2.2f, tile.y * cellSize);
            marker.transform.localScale = Vector3.one * 0.35f;

            Renderer renderer = marker.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.5f);
            renderer.material = mat;

            marker.SetActive(false);

            beacons.Add(new Beacon { tile = tile, biomeType = biomeType, marker = marker });
        }
    }

    private void RefreshVisibility()
    {
        if (!VisualsEnabled)
        {
            foreach (Beacon beacon in beacons) beacon.marker.SetActive(false);
            return;
        }

        AgentStats selected = AgentOverlayUI.SelectedAgent;
        UnityAgent agent = selected != null ? selected.GetComponent<UnityAgent>() : null;

        if (agent == null || agent.agentHandle < 0)
        {
            foreach (Beacon beacon in beacons) beacon.marker.SetActive(false);
            return;
        }

        foreach (Beacon beacon in beacons)
        {
            bool discovered = NativeBridge.IsResourceDiscoveredByAgent(agent.agentHandle, beacon.biomeType, beacon.tile.x, beacon.tile.y) != 0;
            beacon.marker.SetActive(discovered);
        }
    }
}
