using UnityEngine;

public class HouseConstructionSite : MonoBehaviour
{
    public static HouseConstructionSite Instance { get; private set; }

    [Header("Requirements")]
    public int woodRequired = 30; // Total wood needed to finish building the house
    public int currentWoodDeposited = 0;

    public int stoneRequired = 15; // Total stone needed to finish building the house
    public int currentStoneDeposited = 0;

    [Header("Visual Representation")]
    public GameObject houseFinishedModel; // Drag a house prefab or simple cube here
    public GameObject constructionScaffolding; // Visual indicator of progress
    [Tooltip("Shown once combined wood+stone progress crosses halfBuiltThreshold, replacing the scaffolding.")]
    public GameObject houseHalfBuiltModel;
    [Range(0f, 1f)] public float halfBuiltThreshold = 0.5f;

    [Header("Progress UI")]
    [Tooltip("World-space height above this object's origin where the progress bars float.")]
    public float barHeightOffset = 3f;

    private bool isFinished = false;
    private WorldBar woodProgressBar;
    private WorldBar stoneProgressBar;
    private GameObject placeholderMarker;
    private Renderer placeholderRenderer;

    private static readonly Color WoodBarColor = new Color(0.55f, 0.35f, 0.15f);
    private static readonly Color StoneBarColor = new Color(0.55f, 0.55f, 0.58f);
    private static readonly Color BarBackgroundColor = new Color(0.08f, 0.08f, 0.08f);
    private static readonly Color PlaceholderStartColor = new Color(0.6f, 0.5f, 0.3f);
    private static readonly Color PlaceholderDoneColor = new Color(0.3f, 0.8f, 0.3f);

    private void Awake()
    {
        Instance = this;
        if (houseFinishedModel != null) houseFinishedModel.SetActive(false);
        if (houseHalfBuiltModel != null) houseHalfBuiltModel.SetActive(false);
        if (constructionScaffolding != null) constructionScaffolding.SetActive(true);

        EnsurePlaceholderIfNoModels();
        CreateProgressBars();
        UpdateProgressDisplay();
    }

    // If nothing was assigned to any of the three model slots in the
    // Inspector, the site would otherwise have zero visual presence at all —
    // spawn a simple cube marker so its location and rough progress are
    // never fully invisible while a real model gets set up.
    private void EnsurePlaceholderIfNoModels()
    {
        if (constructionScaffolding != null || houseHalfBuiltModel != null || houseFinishedModel != null) return;

        placeholderMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholderMarker.name = "ConstructionSitePlaceholder";
        placeholderMarker.transform.SetParent(transform, false);
        placeholderMarker.transform.localPosition = new Vector3(0f, 1f, 0f);
        placeholderMarker.transform.localScale = new Vector3(2f, 2f, 2f);

        placeholderRenderer = placeholderMarker.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = PlaceholderStartColor;
        placeholderRenderer.material = mat;
    }

    public void DepositWood(int amount)
    {
        if (isFinished) return;

        currentWoodDeposited = Mathf.Min(currentWoodDeposited + amount, woodRequired);
        Debug.Log($"[Construction] Wood Delivered! Current Progress: {currentWoodDeposited}/{woodRequired} wood, {currentStoneDeposited}/{stoneRequired} stone");
        CheckCompletion();
    }

    public void DepositStone(int amount)
    {
        if (isFinished) return;

        currentStoneDeposited = Mathf.Min(currentStoneDeposited + amount, stoneRequired);
        Debug.Log($"[Construction] Stone Delivered! Current Progress: {currentWoodDeposited}/{woodRequired} wood, {currentStoneDeposited}/{stoneRequired} stone");
        CheckCompletion();
    }

    private void CheckCompletion()
    {
        UpdateProgressDisplay();

        if (currentWoodDeposited >= woodRequired && currentStoneDeposited >= stoneRequired)
        {
            CompleteConstruction();
        }
    }

    // Updates the wood/stone bars and switches between scaffolding -> half-built
    // -> finished as combined progress crosses halfBuiltThreshold / 100%.
    private void UpdateProgressDisplay()
    {
        float woodFraction = woodRequired > 0 ? (float)currentWoodDeposited / woodRequired : 1f;
        float stoneFraction = stoneRequired > 0 ? (float)currentStoneDeposited / stoneRequired : 1f;

        if (woodProgressBar != null) woodProgressBar.SetValue(woodFraction);
        if (stoneProgressBar != null) stoneProgressBar.SetValue(stoneFraction);

        float overallFraction = (woodFraction + stoneFraction) * 0.5f;

        if (placeholderRenderer != null)
        {
            placeholderRenderer.material.color = isFinished
                ? PlaceholderDoneColor
                : Color.Lerp(PlaceholderStartColor, PlaceholderDoneColor, overallFraction);
        }

        if (isFinished) return;

        bool shouldBeHalfBuilt = overallFraction >= halfBuiltThreshold;

        if (houseHalfBuiltModel != null) houseHalfBuiltModel.SetActive(shouldBeHalfBuilt);
        if (constructionScaffolding != null) constructionScaffolding.SetActive(!shouldBeHalfBuilt);
    }

    private void CompleteConstruction()
    {
        isFinished = true;
        Debug.Log("[Construction] The house is complete! Amazing teamwork!");

        if (houseFinishedModel != null) houseFinishedModel.SetActive(true);
        if (constructionScaffolding != null) constructionScaffolding.SetActive(false);
        if (houseHalfBuiltModel != null) houseHalfBuiltModel.SetActive(false);
    }

    private void CreateProgressBars()
    {
        GameObject container = new GameObject("ConstructionProgressBars");
        float height = Mathf.Max(barHeightOffset, ComputeModelClearanceHeight());
        ScaleCompensation.Attach(container, transform, new Vector3(0f, height, 0f));
        container.AddComponent<BillboardToCamera>();

        woodProgressBar = CreateBar(container.transform, new Vector3(0f, 0.15f, 0f), WoodBarColor);
        stoneProgressBar = CreateBar(container.transform, new Vector3(0f, -0.1f, 0f), StoneBarColor);
    }

    // barHeightOffset is a guess; if any construction-stage model is actually
    // taller than that, the bars would float inside its geometry — hidden
    // behind walls/scaffolding from most camera angles — rather than above
    // it. This checks every stage's renderer bounds (including the inactive
    // ones) and clears whichever is tallest.
    private float ComputeModelClearanceHeight()
    {
        float tallest = 0f;
        GameObject[] stages = { constructionScaffolding, houseHalfBuiltModel, houseFinishedModel };

        foreach (GameObject stage in stages)
        {
            if (stage == null) continue;
            foreach (Renderer renderer in stage.GetComponentsInChildren<Renderer>(true))
            {
                float topAboveOrigin = renderer.bounds.max.y - transform.position.y;
                if (topAboveOrigin > tallest) tallest = topAboveOrigin;
            }
        }

        return tallest > 0f ? tallest + 0.75f : 0f;
    }

    private WorldBar CreateBar(Transform parent, Vector3 localPos, Color fillColor)
    {
        GameObject barObj = new GameObject("Bar");
        barObj.transform.SetParent(parent, false);
        barObj.transform.localPosition = localPos;

        WorldBar bar = barObj.AddComponent<WorldBar>();
        bar.Initialize(2f, 0.2f, BarBackgroundColor, fillColor);
        return bar;
    }
}
