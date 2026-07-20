using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProceduralTerrain;

// Silent, purely visual pulse on every food resource tile — the on-screen
// indicator for a mechanic that already exists natively and doesn't depend
// on this component at all: PathfinderCore/spatial/Perception.h's
// UpdatePhysicalSenses runs a hearing-radius sweep every time
// NativeBridge.AgentPerceive is called, setting discoveredFood true for any
// food tile within an agent's hearingRange even without line of sight.
// hearingRange is 0 for Deaf agents, so they get nothing from it — that's
// the actual "hearing impaired can't pick up on this" rule, enforced in
// C++, not here. This component never plays audio; the pulse is cosmetic.
public class FoodSoundCue : MonoBehaviour
{
    [Tooltip("Random range (seconds) between visual pulses for a single food tile.")]
    public Vector2 pulseIntervalRange = new Vector2(3f, 6f);

    private class Emitter
    {
        public Transform blipVisual;
        public float timer;
        public float baseScale;
    }

    private readonly List<Emitter> emitters = new List<Emitter>();
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

        foreach (Vector2Int tile in foodTiles)
        {
            SpawnEmitter(tile, generator.cellSize);
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
            // Staggered starting timer so every food tile doesn't pulse in unison.
            timer = Random.Range(pulseIntervalRange.x, pulseIntervalRange.y),
            baseScale = blip.transform.localScale.x,
        });
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
