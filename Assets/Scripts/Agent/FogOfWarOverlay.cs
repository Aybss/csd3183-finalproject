using UnityEngine;
using ProceduralTerrain;

// Renders SLAM's fog-of-war directly on the terrain: one flat quad spanning
// the whole map, textured with a per-tile mask that's dark over tiles the
// currently-selected agent (AgentOverlayUI.SelectedAgent) hasn't discovered
// yet and transparent over tiles it has. Built as a single dynamically
// updated texture rather than one GameObject per tile (which would mean
// thousands of objects on a 50x50 map) — one native call
// (NativeBridge.GetExploredTiles) refreshes the whole thing at once.
//
// Hidden when no agent is selected. Auto-attached by AgentOverlayUI, so no
// extra manual scene setup is needed.
public class FogOfWarOverlay : MonoBehaviour
{
    [Tooltip("How often to re-pull the selected agent's explored-tiles bitmap, in seconds.")]
    public float refreshInterval = 0.25f;
    [Range(0, 255)] public byte fogOpacity = 235;

    private Texture2D fogTexture;
    private Color32[] pixelBuffer;
    private GameObject quadObject;
    private Material fogMaterial;

    private int mapWidth;
    private int mapHeight;
    private bool initialized;
    private float refreshTimer;

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
            return;
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = refreshInterval;

        RefreshFog();
    }

    private void TryInitialize()
    {
        PrimsTerrainGenerator generator = FindObjectOfType<PrimsTerrainGenerator>();
        if (generator == null) return;

        mapWidth = generator.width;
        mapHeight = generator.height;
        if (mapWidth <= 0 || mapHeight <= 0) return;

        BuildFogQuad(generator.cellSize);
        initialized = true;
    }

    private void BuildFogQuad(float cellSize)
    {
        fogTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false);
        fogTexture.filterMode = FilterMode.Point; // crisp per-tile edges, not blurred
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        pixelBuffer = new Color32[mapWidth * mapHeight];
        Color32 hidden = new Color32(0, 0, 0, fogOpacity);
        for (int i = 0; i < pixelBuffer.Length; i++) pixelBuffer[i] = hidden;
        fogTexture.SetPixels32(pixelBuffer);
        fogTexture.Apply();

        // Explicit mesh instead of the built-in Quad primitive, so the world
        // <-> UV mapping is exact and doesn't depend on guessing a rotation's
        // sign — vertex (minX,minZ) is UV (0,0), matching grid tile (0,0),
        // matching pixelBuffer index 0.
        quadObject = new GameObject("FogOfWarOverlay");
        quadObject.transform.SetParent(transform, false);

        float minX = -cellSize * 0.5f;
        float maxX = (mapWidth - 1) * cellSize + cellSize * 0.5f;
        float minZ = -cellSize * 0.5f;
        float maxZ = (mapHeight - 1) * cellSize + cellSize * 0.5f;
        float fogY = 0.2f; // just above bare ground/path tiles

        Mesh mesh = new Mesh { name = "FogOfWarMesh" };
        mesh.vertices = new Vector3[]
        {
            new Vector3(minX, fogY, minZ),
            new Vector3(maxX, fogY, minZ),
            new Vector3(minX, fogY, maxZ),
            new Vector3(maxX, fogY, maxZ),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };
        // Both winding orders, so the quad is visible regardless of which
        // way the active render pipeline culls back-faces by default.
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter filter = quadObject.AddComponent<MeshFilter>();
        filter.mesh = mesh;

        MeshRenderer renderer = quadObject.AddComponent<MeshRenderer>();
        // Sprites/Default, not Unlit/Transparent: shaders only ever reached
        // via Shader.Find (never referenced by a serialized asset) get
        // stripped from a real build by Unity's build-time shader analysis —
        // the Editor has every shader loaded so this never shows up there.
        // Sprites/Default is already guaranteed present (it's in this
        // project's Graphics Settings "Always Included Shaders" list) and
        // handles a textured, per-pixel-alpha quad exactly as well.
        fogMaterial = new Material(Shader.Find("Sprites/Default"));
        fogMaterial.mainTexture = fogTexture;
        renderer.material = fogMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        quadObject.SetActive(false);
    }

    private void RefreshFog()
    {
        AgentStats selected = AgentOverlayUI.SelectedAgent;
        UnityAgent agent = selected != null ? selected.GetComponent<UnityAgent>() : null;

        if (agent == null || agent.agentHandle < 0)
        {
            if (quadObject.activeSelf) quadObject.SetActive(false);
            return;
        }

        if (!quadObject.activeSelf) quadObject.SetActive(true);

        // Refresh every interval regardless of whether it's the same agent
        // as last time — that agent's memory keeps growing as it explores,
        // so "nothing changed" can never be assumed here.
        ApplyExploredTiles(agent.agentHandle);
    }

    private void ApplyExploredTiles(int agentHandle)
    {
        bool[] explored = NativeBridge.GetExploredTiles(agentHandle, mapWidth, mapHeight);
        Color32 hidden = new Color32(0, 0, 0, fogOpacity);
        Color32 revealed = new Color32(0, 0, 0, 0);

        for (int i = 0; i < pixelBuffer.Length && i < explored.Length; i++)
        {
            pixelBuffer[i] = explored[i] ? revealed : hidden;
        }

        fogTexture.SetPixels32(pixelBuffer);
        fogTexture.Apply();
    }
}
