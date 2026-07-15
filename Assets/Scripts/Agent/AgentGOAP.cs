using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(UnityAgent))]
[RequireComponent(typeof(AgentStats))]
public class AgentGOAP : MonoBehaviour
{
    private UnityAgent movementController;
    private AgentStats stats;

    private Vector2Int currentTargetTile = new Vector2Int(-1, -1);
    private string currentAction = "Idle";

    private float startDelayTimer = 3.0f;
    private float thoughtCooldown = 0f;
    private bool isActivated = false;

    private List<Vector2Int> unreachableTiles = new List<Vector2Int>();
    private int mapWidth = 50;
    private int mapHeight = 50;

    private void Start()
    {
        movementController = GetComponent<UnityAgent>();
        stats = GetComponent<AgentStats>();

        var generator = Object.FindObjectOfType<ProceduralTerrain.PrimsTerrainGenerator>();
        if (generator != null)
        {
            mapWidth = generator.width;
            mapHeight = generator.height;
        }
    }

    private void Update()
    {
        if (stats.isDead) return;

        if (startDelayTimer > 0f)
        {
            startDelayTimer -= Time.deltaTime;
            return;
        }

        if (thoughtCooldown > 0f)
        {
            thoughtCooldown -= Time.deltaTime;
            return;
        }

        int currentX = Mathf.RoundToInt(transform.position.x / movementController.cellSize);
        int currentY = Mathf.RoundToInt(transform.position.z / movementController.cellSize);
        Vector2Int currentGridPos = new Vector2Int(currentX, currentY);

        if (currentGridPos == currentTargetTile && currentTargetTile != new Vector2Int(-1, -1))
        {
            ExecuteAtDestination();
            currentTargetTile = new Vector2Int(-1, -1);
            currentAction = "Idle";
        }

        if (currentTargetTile == new Vector2Int(-1, -1))
        {
            PlanNextAction(currentGridPos);
        }
    }

    private void PlanNextAction(Vector2Int currentGridPos)
    {
        if (stats.thirst > 75f) NavigateToResource(currentGridPos, 1, "Drinking");
        else if (stats.hunger > 80f) NavigateToResource(currentGridPos, 3, "Eating");
        else if (stats.fatigue > 85f) NavigateToCamp(currentGridPos);
        else if (stats.woodCarrying > 0) NavigateToBuildSite(currentGridPos);
        else NavigateToResource(currentGridPos, 2, "Chopping");
    }

    private void ExecuteAtDestination()
    {
        switch (currentAction)
        {
            case "Drinking": stats.DrinkWater(); break;
            case "Eating":
                stats.ConsumeFood();
                DestroyResourceAt(currentTargetTile);
                break;
            case "Sleeping": stats.Rest(); break;
            case "Building":
                if (HouseConstructionSite.Instance != null) HouseConstructionSite.Instance.DepositWood(stats.woodCarrying);
                stats.woodCarrying = 0;
                break;
            case "Chopping":
                stats.woodCarrying = stats.maxWoodCapacity;
                DestroyResourceAt(currentTargetTile);
                break;
        }
    }

    private void NavigateToResource(Vector2Int startPos, int biomeType, string actionName)
    {
        Vector2Int target = GetNearestResource(startPos, biomeType);

        // Clamp to prevent out-of-bounds pathing requests to the DLL[cite: 11]
        target.x = Mathf.Clamp(target.x, 0, mapWidth - 1);
        target.y = Mathf.Clamp(target.y, 0, mapHeight - 1);

        if (target == new Vector2Int(-1, -1)) { Wander(startPos); return; }

        currentTargetTile = target;
        currentAction = actionName;

        if (!movementController.SetNewDestination(startPos, currentTargetTile))
        {
            unreachableTiles.Add(currentTargetTile);
            Wander(startPos);
        }
    }

    private Vector2Int GetNearestResource(Vector2Int startPos, int resourceType)
    {
        Vector2Int target = new Vector2Int(-1, -1);
        float minDistance = float.MaxValue;

        var generator = Object.FindObjectOfType<ProceduralTerrain.PrimsTerrainGenerator>();
        if (generator == null) return target;

        foreach (Transform child in generator.transform)
        {
            bool isTarget = (resourceType == 2 && child.name.ToLower().Contains("tree")) ||
                            (resourceType == 3 && child.name.ToLower().Contains("bush"));

            if (isTarget)
            {
                Vector2Int pos = new Vector2Int(Mathf.RoundToInt(child.position.x), Mathf.RoundToInt(child.position.z));
                float dist = Vector2Int.Distance(startPos, pos);
                if (dist < minDistance && !unreachableTiles.Contains(pos))
                {
                    minDistance = dist;
                    target = pos;
                }
            }
        }
        return target;
    }

    private void DestroyResourceAt(Vector2Int pos)
    {
        var generator = Object.FindObjectOfType<ProceduralTerrain.PrimsTerrainGenerator>();
        foreach (Transform child in generator.transform)
        {
            if (Mathf.RoundToInt(child.position.x) == pos.x && Mathf.RoundToInt(child.position.z) == pos.y)
            {
                Destroy(child.gameObject);
                break;
            }
        }
    }

    private void Wander(Vector2Int startPos)
    {
        currentTargetTile = new Vector2Int(
            Mathf.Clamp(startPos.x + Random.Range(-3, 4), 0, mapWidth - 1),
            Mathf.Clamp(startPos.y + Random.Range(-3, 4), 0, mapHeight - 1)
        );
        currentAction = "Wandering";
        movementController.SetNewDestination(startPos, currentTargetTile);
        thoughtCooldown = 0.5f;
    }

    private void NavigateToCamp(Vector2Int startPos)
    {
        currentTargetTile = new Vector2Int(2, 2);
        currentAction = "Sleeping";
        if (!movementController.SetNewDestination(startPos, currentTargetTile)) Wander(startPos);
    }

    private void NavigateToBuildSite(Vector2Int startPos)
    {
        currentTargetTile = new Vector2Int(15, 15);
        currentAction = "Building";
        if (!movementController.SetNewDestination(startPos, currentTargetTile)) Wander(startPos);
    }
}