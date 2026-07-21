using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProceduralTerrain;

// A silent visual pulse on every food resource tile that also does two real
// things for non-Deaf agents each time it fires: PathfinderCore/spatial/
// Perception.h's UpdatePhysicalSenses already reveals food specifically the
// instant a hearing-capable agent gets within its own hearingRange (no line
// of sight needed). This component adds a second, food-initiated channel on
// top of that: when a food tile pulses, it broadcasts outward and clears
// fog-of-war in a radius around itself (not just the food tile) for every
// non-Deaf agent currently within earshot, via NativeBridge.RevealAreaForAgent
// — permanently, the same as a sight sweep, matching how AgentMemory never
// forgets elsewhere in this codebase. Deaf agents (hearingRange <= 0) are
// filtered out natively inside RevealAreaForAgent itself, not here.
public class FoodSoundCue : MonoBehaviour
{
    [Tooltip("Random range (seconds) between visual pulses for a single food tile.")]
    public Vector2 pulseIntervalRange = new Vector2(3f, 6f);
    [Tooltip("How far (in grid tiles) a pulse's fog-of-war reveal reaches around the food tile.")]
    public int revealRadius = 3;
    [Tooltip("How far away (in grid tiles) an agent can be from the food and still be reached by its pulse.")]
    public float broadcastRadius = 10f;

    private class Emitter
    {
        public Transform blipVisual;
        public Vector2Int gridTile;
        public float timer;
        public float baseScale;
    }

    private readonly List<Emitter> emitters = new List<Emitter>();
    private float cellSize = 1f;
    private bool initialized;

    // Called after a map regenerates — old emitters point at food tiles that
    // may no longer exist (or exist somewhere else entirely).
    public void ResetEmitters()
    {
        // Stop any in-flight PulseVisual coroutines first — they hold their
        // own captured Transform reference independent of the emitters list
        // below, and would otherwise resume next frame and throw a
        // MissingReferenceException against the Transform this is about to destroy.
        StopAllCoroutines();

        foreach (Emitter emitter in emitters)
        {
            if (emitter.blipVisual != null) Destroy(emitter.blipVisual.gameObject);
        }
        emitters.Clear();
        initialized = false;
    }

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
            return;
        }

        foreach (Emitter emitter in emitters)
        {
            emitter.timer -= Time.deltaTime;
            if (emitter.timer <= 0f)
            {
                emitter.timer = Random.Range(pulseIntervalRange.x, pulseIntervalRange.y);
                StartCoroutine(PulseVisual(emitter));
                BroadcastRevealToNearbyAgents(emitter.gridTile);
            }
        }
    }

    // Terrain generation can finish after this component's own Start(), so
    // keep retrying each Update until food tiles actually exist.
    private void TryInitialize()
    {
        PrimsTerrainGenerator generator = FindObjectOfType<PrimsTerrainGenerator>();
        if (generator == null) return;

        List<Vector2Int> foodTiles = generator.FindResourceTiles(BiomeType.Food);
        if (foodTiles.Count == 0) return;

        cellSize = generator.cellSize;
        foreach (Vector2Int tile in foodTiles)
        {
            SpawnEmitter(tile, cellSize);
        }

        initialized = true;
    }

    private void SpawnEmitter(Vector2Int tile, float cellSize)
    {
        GameObject blip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blip.name = $"FoodSoundCue_{tile.x}_{tile.y}";
        Destroy(blip.GetComponent<Collider>());
        blip.transform.position = new Vector3(tile.x * cellSize, 1.6f, tile.y * cellSize);
        blip.transform.localScale = Vector3.one * 0.25f;

        Color color = new Color(0.95f, 0.85f, 0.2f);
        Renderer renderer = blip.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.3f); // dim at rest, flares on pulse
        renderer.material = mat;

        emitters.Add(new Emitter
        {
            blipVisual = blip.transform,
            gridTile = tile,
            // Staggered starting timer so every food tile doesn't pulse in unison.
            timer = Random.Range(pulseIntervalRange.x, pulseIntervalRange.y),
            baseScale = blip.transform.localScale.x,
        });
    }

    // Finds every active agent within broadcastRadius of the pulsing food
    // tile and asks the native plugin to reveal the area around it in that
    // agent's own memory. Deaf agents are filtered out inside
    // NativeBridge.RevealAreaForAgent itself, not here — every agent found
    // nearby gets the call, harmlessly no-opping for the ones who can't hear.
    private void BroadcastRevealToNearbyAgents(Vector2Int foodTile)
    {
        foreach (UnityAgent agent in FindObjectsOfType<UnityAgent>())
        {
            if (agent.agentHandle < 0) continue;

            Vector2Int agentTile = new Vector2Int(
                Mathf.RoundToInt(agent.transform.position.x / cellSize),
                Mathf.RoundToInt(agent.transform.position.z / cellSize));

            if (Vector2Int.Distance(agentTile, foodTile) > broadcastRadius) continue;

            NativeBridge.RevealAreaForAgent(agent.agentHandle, foodTile.x, foodTile.y, revealRadius);
        }
    }

    private IEnumerator PulseVisual(Emitter emitter)
    {
        Transform t = emitter.blipVisual;
        if (t == null) yield break;

        const float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (t == null) yield break; // destroyed mid-pulse (e.g. map regenerated)
            elapsed += Time.deltaTime;
            float pulse = Mathf.Sin((elapsed / duration) * Mathf.PI); // 0 -> 1 -> 0
            t.localScale = Vector3.one * emitter.baseScale * (1f + pulse * 0.8f);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one * emitter.baseScale;
    }
}
