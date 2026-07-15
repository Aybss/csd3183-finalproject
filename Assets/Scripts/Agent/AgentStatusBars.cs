using UnityEngine;

// Floating hunger/thirst/fatigue meters above each agent. Bars fill up as
// the stat rises toward its critical threshold (100) — a fuller bar means
// more urgent. Freezes once the agent dies; AgentDeathVisual handles the
// death indicator.
public class AgentStatusBars : MonoBehaviour
{
    private AgentStats stats;
    private WorldBar hungerBar;
    private WorldBar thirstBar;
    private WorldBar fatigueBar;

    private static readonly Color HungerColor = new Color(0.85f, 0.55f, 0.1f);
    private static readonly Color ThirstColor = new Color(0.2f, 0.55f, 0.9f);
    private static readonly Color FatigueColor = new Color(0.45f, 0.35f, 0.85f);
    private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.08f);

    public void Initialize(AgentStats stats, float anchorHeight)
    {
        this.stats = stats;

        GameObject container = new GameObject("StatusBars");
        container.transform.SetParent(transform, false);
        container.transform.localPosition = new Vector3(0f, anchorHeight, 0f);
        container.AddComponent<BillboardToCamera>();

        hungerBar = CreateBar(container.transform, new Vector3(0f, 0.24f, 0f), HungerColor);
        thirstBar = CreateBar(container.transform, new Vector3(0f, 0.05f, 0f), ThirstColor);
        fatigueBar = CreateBar(container.transform, new Vector3(0f, -0.14f, 0f), FatigueColor);
    }

    private WorldBar CreateBar(Transform parent, Vector3 localPos, Color fillColor)
    {
        GameObject barObj = new GameObject("Bar");
        barObj.transform.SetParent(parent, false);
        barObj.transform.localPosition = localPos;

        WorldBar bar = barObj.AddComponent<WorldBar>();
        bar.Initialize(1.4f, 0.12f, BackgroundColor, fillColor);
        return bar;
    }

    private void Update()
    {
        if (stats == null || stats.isDead) return;
        hungerBar.SetValue(stats.hunger / 100f);
        thirstBar.SetValue(stats.thirst / 100f);
        fatigueBar.SetValue(stats.fatigue / 100f);
    }
}
