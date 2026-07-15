using UnityEngine;

// Marks a dead agent with a red X across its body instead of letting it
// vanish — AgentStats no longer destroys the GameObject on death, so the
// corpse stays visible as a clear "this one's out" signal during a demo.
public class AgentDeathVisual : MonoBehaviour
{
    private AgentStats stats;
    private bool applied;

    private void Start()
    {
        stats = GetComponent<AgentStats>();
    }

    private void Update()
    {
        if (applied || stats == null || !stats.isDead) return;
        applied = true;
        ShowDeathCross();
    }

    private void ShowDeathCross()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        float height = capsule != null ? capsule.height : 2f;

        GameObject cross = new GameObject("DeathCross");
        ScaleCompensation.Attach(cross, transform, new Vector3(0f, height * 0.5f, 0f));
        cross.AddComponent<BillboardToCamera>();

        CreateBar(cross.transform, 45f, height);
        CreateBar(cross.transform, -45f, height);
    }

    private void CreateBar(Transform parent, float zRotation, float height)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "DeathCrossBar";
        Destroy(bar.GetComponent<Collider>());
        bar.transform.SetParent(parent, false);
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        bar.transform.localScale = new Vector3(height * 0.9f, height * 0.12f, height * 0.12f);

        Renderer renderer = bar.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.red;
        renderer.material = mat;
    }
}
