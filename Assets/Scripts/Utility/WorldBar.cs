using UnityEngine;

// A minimal world-space progress bar built from two flattened cube
// primitives (dark background + colored fill) — no Canvas/UGUI needed, so it
// composes cleanly with the procedural, non-UI visuals already used for
// agent role markers and labels. Reused for both per-agent hunger/fatigue
// meters and the house construction site's wood/stone progress.
public class WorldBar : MonoBehaviour
{
    private Transform fill;
    private float maxWidth;

    public void Initialize(float width, float height, Color backgroundColor, Color fillColor)
    {
        maxWidth = width;

        CreateQuad("Background", width, height, backgroundColor).transform.localPosition = Vector3.zero;

        GameObject fillObj = CreateQuad("Fill", width, height * 0.7f, fillColor);
        fill = fillObj.transform;

        SetValue(0f);
    }

    // t in [0,1]. The fill quad stays left-anchored as it grows/shrinks.
    public void SetValue(float t)
    {
        if (fill == null) return;

        t = Mathf.Clamp01(t);
        float w = Mathf.Max(0.001f, maxWidth * t);
        fill.localScale = new Vector3(w, fill.localScale.y, fill.localScale.z);
        fill.localPosition = new Vector3(-maxWidth * 0.5f + w * 0.5f, 0f, -0.01f);
    }

    private GameObject CreateQuad(string name, float width, float height, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>()); // purely cosmetic, no physics needed
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(width, height, 0.02f);

        Renderer renderer = go.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.material = mat;

        return go;
    }
}
