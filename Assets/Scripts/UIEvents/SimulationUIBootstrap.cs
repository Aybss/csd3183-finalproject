using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Instantiates the SimulationSettingsUI prefab at runtime — it was only ever
// placed in separate test scenes (UI-Test.unity, TestPrefabs.unity), never
// in the actual gameplay scene, which is why it never showed up there. This
// avoids hand-editing TerrainTestScene's YAML to drop a ~6500-line prefab
// in; everything is wired from code instead, matching how the rest of this
// session's UI (AgentOverlayUI) was built.
//
// Adds a small always-visible tab that collapses/expands the settings
// panel — it's anchored top-right so it doesn't compete with
// AgentOverlayUI's top-left panel, but 400x1080 is still a lot of screen to
// leave open by default.
//
// Add this component to any single GameObject in the scene.
public class SimulationUIBootstrap : MonoBehaviour
{
    [Tooltip("Panel starts collapsed so it doesn't clutter the screen until you want it.")]
    public bool startCollapsed = true;

    public SimulationUIController Controller { get; private set; }

    private GameObject settingsPanel;

    private void Awake()
    {
        EnsureEventSystem();

        GameObject canvasObj = new GameObject("SimulationUICanvas");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // The prefab was authored against a 1920x1080 reference (its root
        // RectTransform and SettingsPanel are both sized/positioned assuming
        // that). Without this, CanvasScaler defaults to ConstantPixelSize —
        // 1 canvas unit = 1 screen pixel — so a 1080-tall panel clips on any
        // window/Game-view shorter than 1080px. Matching on height keeps the
        // whole panel on-screen at any resolution, scaled proportionally.
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // 1 = match height

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject prefab = Resources.Load<GameObject>("SimulationSettingsUI");
        if (prefab == null)
        {
            Debug.LogError("[SimulationUIBootstrap] Couldn't load SimulationSettingsUI — expected it at Assets/Resources/SimulationSettingsUI.prefab.");
            return;
        }

        GameObject instance = Instantiate(prefab, canvasObj.transform, false);
        Controller = instance.GetComponent<SimulationUIController>();

        settingsPanel = FindChildByName(instance.transform, "SettingsPanel");
        if (settingsPanel != null) settingsPanel.SetActive(!startCollapsed);

        BuildCollapseTab(canvasObj.transform);
    }

    private void BuildCollapseTab(Transform parent)
    {
        GameObject tabObj = new GameObject("SettingsToggleTab");
        tabObj.transform.SetParent(parent, false);

        RectTransform rect = tabObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-8f, -8f);
        rect.sizeDelta = new Vector2(100f, 28f);

        Image bg = tabObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        Button button = tabObj.AddComponent<Button>();
        button.targetGraphic = bg;

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(tabObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 13;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = (settingsPanel != null && settingsPanel.activeSelf) ? "Settings ▴" : "Settings ▾";

        button.onClick.AddListener(() =>
        {
            if (settingsPanel == null) return;
            bool nowVisible = !settingsPanel.activeSelf;
            settingsPanel.SetActive(nowVisible);
            label.text = nowVisible ? "Settings ▴" : "Settings ▾";
        });
    }

    private GameObject FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child.gameObject;
        }
        return null;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<EventSystem>();
        esObj.AddComponent<StandaloneInputModule>();
    }
}
