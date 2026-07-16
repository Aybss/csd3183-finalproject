using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Screen-space overlay (top-left corner) listing every agent's role, hunger,
// and fatigue, plus a small always-visible house construction progress
// header (a guaranteed-visible fallback for the world-space bars floating
// over the construction site, which depend on that site's transform scale
// and model height being reasonable). Clicking a row pans the scene camera
// to lock onto that agent; clicking again (or "Free Camera") releases it.
// Built entirely from code — no scene/prefab setup beyond adding this
// component to one GameObject.
public class AgentOverlayUI : MonoBehaviour
{
    [Tooltip("Re-scan for agents this often, in seconds — agents can spawn after this overlay starts.")]
    public float rescanInterval = 1f;
    [Tooltip("Visible height of the scrollable agent list, in pixels, before it scrolls instead of growing.")]
    public float listViewportHeight = 260f;

    private RectTransform contentRoot;
    private readonly Dictionary<AgentStats, OverlayRow> rows = new Dictionary<AgentStats, OverlayRow>();
    private float rescanTimer;

    private FreeFlyCamera cameraController;
    private AgentStats selectedStats;

    private BarWidget houseWoodBar;
    private BarWidget houseStoneBar;

    private static readonly Color HungerColor = new Color(0.85f, 0.55f, 0.1f);
    private static readonly Color ThirstColor = new Color(0.2f, 0.55f, 0.9f);
    private static readonly Color FatigueColor = new Color(0.45f, 0.35f, 0.85f);
    private static readonly Color DeadColor = new Color(0.4f, 0.4f, 0.4f);
    private static readonly Color BarBackground = new Color(0.15f, 0.15f, 0.15f);
    private static readonly Color RowBackground = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color SelectedRowBackground = new Color(0.2f, 0.35f, 0.55f, 0.85f);
    private static readonly Color WoodColor = new Color(0.55f, 0.35f, 0.15f);
    private static readonly Color StoneColor = new Color(0.55f, 0.55f, 0.58f);

    // A fill bar with its own centered text label baked in (e.g. "Food 42%")
    // so what the bar means and its exact value are never ambiguous from
    // color/fill alone.
    private class BarWidget
    {
        public Image fill;
        public Text text;
        public string prefix;
    }

    private class OverlayRow
    {
        public GameObject root;
        public Image background;
        public Text label;
        public Text actionText;
        public AgentGOAP goap;
        public string roleName;
        public BarWidget hungerBar;
        public BarWidget thirstBar;
        public BarWidget fatigueBar;
        public bool deadShown;
    }

    // Other systems (e.g. SLAM discovery beacons) read this to know which
    // agent's knowledge to visualize.
    public static AgentStats SelectedAgent { get; private set; }

    private void Awake()
    {
        // Image (including Filled-type fill bars) renders fine as a plain
        // colored rectangle with no sprite assigned — every Image below
        // just uses `color`.
        EnsureEventSystem();
        BuildUI();
        RescanAgents();

        // Piggyback the SLAM beacon visualizer and fog-of-war overlay onto
        // this same GameObject so selecting an agent here is the only setup
        // needed to see either work — no extra manual component to add.
        if (GetComponent<SlamDiscoveryBeacons>() == null) gameObject.AddComponent<SlamDiscoveryBeacons>();
        if (GetComponent<FogOfWarOverlay>() == null) gameObject.AddComponent<FogOfWarOverlay>();
    }

    private void Update()
    {
        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = rescanInterval;
            RescanAgents();
        }

        RemoveStaleRows();

        foreach (var pair in rows)
        {
            AgentStats stats = pair.Key;
            OverlayRow row = pair.Value;
            if (stats == null) continue;

            row.background.color = (stats == selectedStats) ? SelectedRowBackground : RowBackground;

            if (stats.isDead)
            {
                if (!row.deadShown)
                {
                    row.deadShown = true;
                    row.label.text = $"{row.roleName}  (dead)";
                    SetBarColor(row.hungerBar, DeadColor);
                    SetBarColor(row.thirstBar, DeadColor);
                    SetBarColor(row.fatigueBar, DeadColor);
                }
                continue;
            }

            SetBarValue(row.hungerBar, stats.hunger / 100f);
            SetBarValue(row.thirstBar, stats.thirst / 100f);
            SetBarValue(row.fatigueBar, stats.fatigue / 100f);
            row.label.text = row.roleName;
            if (row.actionText != null && row.goap != null) row.actionText.text = FormatAction(row.goap.CurrentAction);
        }

        UpdateHousePanel();
    }

    private void SetBarValue(BarWidget bar, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        bar.fill.fillAmount = fraction;
        bar.text.text = $"{bar.prefix} {Mathf.RoundToInt(fraction * 100f)}%";
    }

    private void SetBarColor(BarWidget bar, Color color)
    {
        bar.fill.color = color;
    }

    // Turns the raw enum (e.g. NavigateToWood) into a short human-readable
    // phrase — this is the one place you can watch GOAP's live decision.
    private static string FormatAction(AgentAction action)
    {
        switch (action)
        {
            case AgentAction.ExploreUnknown: return "Exploring";
            case AgentAction.NavigateToWood: return "Heading to wood";
            case AgentAction.CollectWood: return "Chopping wood";
            case AgentAction.NavigateToFood: return "Heading to food";
            case AgentAction.Eat: return "Eating";
            case AgentAction.DetectFoodBySound: return "Listening for food";
            case AgentAction.NavigateToStone: return "Heading to stone";
            case AgentAction.MineStone: return "Mining stone";
            case AgentAction.NavigateToWater: return "Heading to water";
            case AgentAction.DrinkWater: return "Drinking";
            case AgentAction.NavigateToBuildSite: return "Heading to build site";
            case AgentAction.DeliverWood: return "Delivering wood";
            case AgentAction.DeliverStone: return "Delivering stone";
            case AgentAction.NavigateToCamp: return "Heading to camp";
            case AgentAction.Rest: return "Resting";
            default: return action.ToString();
        }
    }

    private void UpdateHousePanel()
    {
        HouseConstructionSite site = HouseConstructionSite.Instance;
        if (site == null || houseWoodBar == null) return;

        float woodFrac = site.woodRequired > 0 ? (float)site.currentWoodDeposited / site.woodRequired : 1f;
        float stoneFrac = site.stoneRequired > 0 ? (float)site.currentStoneDeposited / site.stoneRequired : 1f;

        houseWoodBar.fill.fillAmount = Mathf.Clamp01(woodFrac);
        houseWoodBar.text.text = $"Wood {site.currentWoodDeposited}/{site.woodRequired}";
        houseStoneBar.fill.fillAmount = Mathf.Clamp01(stoneFrac);
        houseStoneBar.text.text = $"Stone {site.currentStoneDeposited}/{site.stoneRequired}";
    }

    private void RescanAgents()
    {
        AgentStats[] found = FindObjectsOfType<AgentStats>();
        foreach (AgentStats stats in found)
        {
            if (rows.ContainsKey(stats)) continue;
            rows[stats] = CreateRow(stats);
        }
    }

    // Destroyed agents (e.g. from a simulation Restart/Random Map) would
    // otherwise leave their row stuck in the list forever, since nothing
    // else ever removes entries from `rows`.
    private void RemoveStaleRows()
    {
        List<AgentStats> stale = null;
        foreach (var pair in rows)
        {
            if (pair.Key != null) continue; // Unity fake-null check: still-alive vs destroyed
            stale ??= new List<AgentStats>();
            stale.Add(pair.Key);
            if (pair.Value.root != null) Destroy(pair.Value.root);
        }

        if (stale == null) return;

        foreach (AgentStats key in stale)
        {
            rows.Remove(key);
            // ReferenceEquals, not ==, since Unity's == on a destroyed object
            // already reads as "null" and would make a plain comparison here
            // always miss.
            if (System.Object.ReferenceEquals(key, selectedStats)) Deselect();
        }
    }

    private void EnsureEventSystem()
    {
        // Button clicks (row selection, Free Camera) need an EventSystem in
        // the scene — create one if the project doesn't already have one,
        // so this overlay works without any extra manual scene setup.
        if (FindObjectOfType<EventSystem>() != null) return;

        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<EventSystem>();
        esObj.AddComponent<StandaloneInputModule>();
    }

    private void BuildUI()
    {
        GameObject canvasObj = new GameObject("AgentOverlayCanvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("OverlayPanel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(12f, -12f);
        panelRect.sizeDelta = new Vector2(220f, 0f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing = 6f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        ContentSizeFitter panelFitter = panel.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildLegendPanel(panel.transform);
        BuildHousePanel(panel.transform);
        BuildToolbar(panel.transform);
        BuildScrollableList(panel.transform);
    }

    // Answers "how do these agents actually differ" directly in text, since
    // color/shape alone only tells you they're different, not how.
    private void BuildLegendPanel(Transform parent)
    {
        GameObject legendObj = new GameObject("LegendPanel");
        legendObj.transform.SetParent(parent, false);

        Image bg = legendObj.AddComponent<Image>();
        bg.color = RowBackground;

        VerticalLayoutGroup layout = legendObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = legendObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText(legendObj.transform, "Impairments", 14, FontStyle.Bold);
        CreateWrappedText(legendObj.transform, "Wheelchair (blue): rubble near stone blocks it, moves slower overall");
        CreateWrappedText(legendObj.transform, "Blind (red): sees only 1 tile, hears food from far away, explores slowly");
        CreateWrappedText(legendObj.transform, "Deaf (orange): normal sight range, ignores sound-based path costs");
    }

    private void CreateWrappedText(Transform parent, string text)
    {
        Text label = CreateText(parent, text, 10, FontStyle.Normal);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        LayoutElement le = label.GetComponent<LayoutElement>();
        if (le != null) le.preferredHeight = 28f;
    }

    private void BuildHousePanel(Transform parent)
    {
        GameObject houseObj = new GameObject("HousePanel");
        houseObj.transform.SetParent(parent, false);

        Image bg = houseObj.AddComponent<Image>();
        bg.color = RowBackground;

        VerticalLayoutGroup layout = houseObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = houseObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText(houseObj.transform, "House Construction", 14, FontStyle.Bold);
        houseWoodBar = CreateFillBar(houseObj.transform, WoodColor, "Wood");
        houseStoneBar = CreateFillBar(houseObj.transform, StoneColor, "Stone");
    }

    private void BuildToolbar(Transform parent)
    {
        GameObject toolbar = new GameObject("Toolbar");
        toolbar.transform.SetParent(parent, false);

        LayoutElement le = toolbar.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;

        Image bg = toolbar.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f);

        Button button = toolbar.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(Deselect);

        Text label = CreateText(toolbar.transform, "Free Camera", 13, FontStyle.Bold);
        label.alignment = TextAnchor.MiddleCenter;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void BuildScrollableList(Transform parent)
    {
        GameObject scrollObj = new GameObject("AgentScrollView");
        scrollObj.transform.SetParent(parent, false);

        LayoutElement scrollLayoutElement = scrollObj.AddComponent<LayoutElement>();
        scrollLayoutElement.preferredHeight = listViewportHeight;

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        // Default scrollSensitivity (~1) feels almost unresponsive to the
        // mouse wheel; this is a much snappier, commonly-used value.
        scrollRect.scrollSensitivity = 30f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // Mask needs a raycastable graphic
        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("AgentList");
        content.transform.SetParent(viewport.transform, false);
        contentRoot = content.AddComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup listLayout = content.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 6f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter listFitter = content.AddComponent<ContentSizeFitter>();
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;
    }

    private OverlayRow CreateRow(AgentStats stats)
    {
        GameObject rowObj = new GameObject($"Row_{stats.gameObject.name}");
        rowObj.transform.SetParent(contentRoot, false);

        Image rowBg = rowObj.AddComponent<Image>();
        rowBg.color = RowBackground;

        VerticalLayoutGroup rowLayout = rowObj.AddComponent<VerticalLayoutGroup>();
        rowLayout.padding = new RectOffset(8, 8, 6, 6);
        rowLayout.spacing = 3f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;

        ContentSizeFitter rowFitter = rowObj.AddComponent<ContentSizeFitter>();
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UnityAgent agent = stats.GetComponent<UnityAgent>();
        AgentGOAP goap = stats.GetComponent<AgentGOAP>();
        string roleName = agent != null ? ImpairmentVisuals.LabelForRole((AgentRoleType)agent.role) : "Agent";

        Text label = CreateText(rowObj.transform, roleName, 13, FontStyle.Bold);
        Text actionText = CreateText(rowObj.transform, "—", 11, FontStyle.Italic);
        actionText.color = new Color(0.8f, 0.85f, 1f);
        BarWidget hungerBar = CreateFillBar(rowObj.transform, HungerColor, "Hunger");
        BarWidget thirstBar = CreateFillBar(rowObj.transform, ThirstColor, "Thirst");
        BarWidget fatigueBar = CreateFillBar(rowObj.transform, FatigueColor, "Fatigue");

        Button button = rowObj.AddComponent<Button>();
        button.targetGraphic = rowBg;
        button.onClick.AddListener(() => SelectAgent(stats));

        return new OverlayRow
        {
            root = rowObj, background = rowBg, label = label, actionText = actionText, goap = goap,
            roleName = roleName, hungerBar = hungerBar, thirstBar = thirstBar, fatigueBar = fatigueBar
        };
    }

    // Public so SimulationGameplayBridge can trigger the same select+camera-
    // lock behavior from the simulation UI's "View Agent" button.
    public void SelectAgent(AgentStats stats)
    {
        if (stats == null) return;
        if (cameraController == null) cameraController = FindObjectOfType<FreeFlyCamera>();

        if (selectedStats == stats)
        {
            Deselect();
            return;
        }

        selectedStats = stats;
        SelectedAgent = stats;
        if (cameraController != null) cameraController.SetLockedTarget(stats.transform);
    }

    public void Deselect()
    {
        selectedStats = null;
        SelectedAgent = null;
        if (cameraController != null) cameraController.ClearLock();
    }

    private Text CreateText(Transform parent, string text, int fontSize, FontStyle style)
    {
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(parent, false);

        Text label = textObj.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;

        LayoutElement le = textObj.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 6f;
        return label;
    }

    // A bar with a centered text label baked directly on top of it (e.g.
    // "Food 42%") so what it means and its exact value are always legible,
    // independent of how obvious the fill/color alone would be.
    private BarWidget CreateFillBar(Transform parent, Color fillColor, string prefix)
    {
        GameObject barObj = new GameObject("Bar");
        barObj.transform.SetParent(parent, false);

        LayoutElement le = barObj.AddComponent<LayoutElement>();
        le.preferredHeight = 16f;

        Image background = barObj.AddComponent<Image>();
        background.color = BarBackground;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillObj.AddComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(barObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = $"{prefix} 0%";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 11;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);

        return new BarWidget { fill = fill, text = text, prefix = prefix };
    }
}
