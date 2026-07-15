using UnityEngine;

public class HouseConstructionSite : MonoBehaviour
{
    public static HouseConstructionSite Instance { get; private set; }

    [Header("Requirements")]
    public int woodRequired = 30; // Total wood needed to finish building the house
    public int currentWoodDeposited = 0;

    [Header("Visual Representation")]
    public GameObject houseFinishedModel; // Drag a house prefab or simple cube here
    public GameObject constructionScaffolding; // Visual indicator of progress

    private bool isFinished = false;

    private void Awake()
    {
        Instance = this;
        if (houseFinishedModel != null) houseFinishedModel.SetActive(false);
        if (constructionScaffolding != null) constructionScaffolding.SetActive(true);
    }

    public void DepositWood(int amount)
    {
        if (isFinished) return;

        currentWoodDeposited = Mathf.Min(currentWoodDeposited + amount, woodRequired);
        Debug.Log($"[Construction] Wood Delivered! Current Progress: {currentWoodDeposited}/{woodRequired}");

        if (currentWoodDeposited >= woodRequired)
        {
            CompleteConstruction();
        }
    }

    private void CompleteConstruction()
    {
        isFinished = true;
        Debug.Log("[Construction] The house is complete! Amazing teamwork!");

        if (houseFinishedModel != null) houseFinishedModel.SetActive(true);
        if (constructionScaffolding != null) constructionScaffolding.SetActive(false);
    }
}