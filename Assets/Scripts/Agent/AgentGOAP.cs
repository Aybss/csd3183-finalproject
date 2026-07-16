using UnityEngine;
using ProceduralTerrain;

[RequireComponent(typeof(UnityAgent))]
[RequireComponent(typeof(AgentStats))]
public class AgentGOAP : MonoBehaviour
{
    private UnityAgent movementController;
    private AgentStats stats;

    private Vector2Int currentTargetTile = new Vector2Int(-1, -1);
    private AgentAction currentAction = AgentAction.ExploreUnknown;
    private bool hasDestination = false;
    private int reservedBiomeType = -1; // BiomeType of currentTargetTile if it was a claimed resource, else -1

    private float startDelayTimer = 3.0f;
    private float thoughtCooldown = 0f;

    private int mapWidth = 50;
    private int mapHeight = 50;
    private bool deathCleanupDone = false;

    // Read by AgentOverlayUI to show what each agent's GOAP planner actually
    // decided to do this tick — the one concrete, live signal that GOAP is
    // really driving behavior and not just a hardcoded routine.
    public AgentAction CurrentAction => currentAction;

    private void Start()
    {
        movementController = GetComponent<UnityAgent>();
        stats = GetComponent<AgentStats>();

        var generator = Object.FindObjectOfType<PrimsTerrainGenerator>();
        if (generator != null)
        {
            mapWidth = generator.width;
            mapHeight = generator.height;
        }
    }

    private void Update()
    {
        if (stats.isDead)
        {
            // Release any resource this agent had claimed but never reached —
            // otherwise a mid-task death permanently locks that tile away
            // from the rest of the group.
            if (!deathCleanupDone)
            {
                deathCleanupDone = true;
                if (hasDestination && reservedBiomeType != -1)
                {
                    NativeBridge.ReleaseResource(reservedBiomeType, currentTargetTile.x, currentTargetTile.y);
                }
                hasDestination = false;
            }
            return;
        }

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

        Vector2Int currentGridPos = GetCurrentGridPos();

        if (hasDestination && currentGridPos == currentTargetTile)
        {
            hasDestination = false; // arrived — next tick's plan will pick the follow-up action
        }

        if (!hasDestination)
        {
            DecideAndAct(currentGridPos);
        }
    }

    private Vector2Int GetCurrentGridPos()
    {
        int x = Mathf.RoundToInt(transform.position.x / movementController.cellSize);
        int y = Mathf.RoundToInt(transform.position.z / movementController.cellSize);
        return new Vector2Int(x, y);
    }

    private void DecideAndAct(Vector2Int currentGridPos)
    {
        NativeBridge.SyncAgentBlackboard(
            movementController.agentHandle,
            stats.hunger, stats.thirst, stats.fatigue,
            stats.woodCarrying, stats.maxWoodCapacity,
            stats.stoneCarrying, stats.maxStoneCapacity);

        int actionCode = NativeBridge.PlanNextAction(
            movementController.agentHandle, currentGridPos.x, currentGridPos.y,
            out int targetX, out int targetY);

        currentAction = (AgentAction)actionCode;

        if (IsNavigateAction(currentAction))
        {
            Vector2Int target = new Vector2Int(
                Mathf.Clamp(targetX, 0, mapWidth - 1),
                Mathf.Clamp(targetY, 0, mapHeight - 1));

            reservedBiomeType = BiomeTypeForNavigateAction(currentAction);

            if (target == currentGridPos)
            {
                // Nothing reachable to navigate to (e.g. explore with no
                // better target) — treat this tick as a short wander instead
                // of spinning in place.
                Wander(currentGridPos);
                return;
            }

            if (movementController.SetNewDestination(currentGridPos, target))
            {
                currentTargetTile = target;
                hasDestination = true;
            }
            else
            {
                if (reservedBiomeType != -1) NativeBridge.ReleaseResource(reservedBiomeType, target.x, target.y);
                Wander(currentGridPos);
            }
        }
        else
        {
            // Stationary action — the planner only returns these once the
            // native WorldState already sees the agent standing on the right
            // tile, so execute immediately without moving.
            ExecuteStationaryAction(currentAction, currentGridPos);
            thoughtCooldown = 0.15f; // avoid a same-frame replan loop
        }
    }

    private bool IsNavigateAction(AgentAction action)
    {
        switch (action)
        {
            case AgentAction.ExploreUnknown:
            case AgentAction.NavigateToWood:
            case AgentAction.NavigateToFood:
            case AgentAction.NavigateToStone:
            case AgentAction.NavigateToWater:
            case AgentAction.NavigateToBuildSite:
            case AgentAction.NavigateToCamp:
                return true;
            default:
                return false;
        }
    }

    private int BiomeTypeForNavigateAction(AgentAction action)
    {
        switch (action)
        {
            case AgentAction.NavigateToWood: return (int)BiomeType.Wood;
            case AgentAction.NavigateToFood: return (int)BiomeType.Food;
            case AgentAction.NavigateToStone: return (int)BiomeType.Stone;
            default: return -1; // water/build-site/camp/explore aren't reserved resources
        }
    }

    private void ExecuteStationaryAction(AgentAction action, Vector2Int pos)
    {
        switch (action)
        {
            case AgentAction.Eat:
                stats.ConsumeFood();
                HarvestResourceAt(pos, BiomeType.Food);
                break;
            case AgentAction.CollectWood:
                stats.woodCarrying = stats.maxWoodCapacity;
                HarvestResourceAt(pos, BiomeType.Wood);
                break;
            case AgentAction.MineStone:
                stats.stoneCarrying = stats.maxStoneCapacity;
                HarvestResourceAt(pos, BiomeType.Stone);
                break;
            case AgentAction.DrinkWater:
                stats.DrinkWaterInstant();
                break;
            case AgentAction.Rest:
                stats.Rest();
                break;
            case AgentAction.DeliverWood:
                if (HouseConstructionSite.Instance != null) HouseConstructionSite.Instance.DepositWood(stats.woodCarrying);
                stats.woodCarrying = 0;
                break;
            case AgentAction.DeliverStone:
                if (HouseConstructionSite.Instance != null) HouseConstructionSite.Instance.DepositStone(stats.stoneCarrying);
                stats.stoneCarrying = 0;
                break;
            case AgentAction.DetectFoodBySound:
                // No direct gameplay effect — food discovery already happens
                // via the hearing sweep inside NativeBridge.AgentPerceive.
                // Standing still for a beat is the "listening" this represents.
                break;
        }
    }

    // Destroys the visual prop at `pos` and tells native the tile is depleted
    // (clears the WorldGrid layer + every agent's stale memory of it, and
    // frees the reservation so nobody's stuck waiting on a gone resource).
    private void HarvestResourceAt(Vector2Int pos, BiomeType type)
    {
        NativeBridge.ClearResourceTile((int)type, pos.x, pos.y);

        var generator = Object.FindObjectOfType<PrimsTerrainGenerator>();
        if (generator == null) return;

        string needle = type switch
        {
            BiomeType.Wood => "tree",
            BiomeType.Food => "food",
            BiomeType.Stone => null, // rock_tall_a/b, stone_large_a/b — matched by generic fallback below
            _ => null
        };

        foreach (Transform child in generator.transform)
        {
            Vector2Int childPos = new Vector2Int(
                Mathf.RoundToInt(child.position.x / generator.cellSize),
                Mathf.RoundToInt(child.position.z / generator.cellSize));

            if (Vector2Int.Distance(childPos, pos) > 1.0f) continue;

            bool nameMatches = needle != null
                ? child.name.ToLower().Contains(needle)
                : (child.name.ToLower().Contains("rock_tall") || child.name.ToLower().Contains("stone_large"));

            if (nameMatches)
            {
                Destroy(child.gameObject);
                NativeBridge.SetBlocked(pos.x, pos.y, 0);
                return;
            }
        }
    }

    private void Wander(Vector2Int startPos)
    {
        currentAction = AgentAction.ExploreUnknown;

        // Sample a few random nearby offsets and only commit to one that's
        // actually walkable — avoids spamming a pathfind failure (and its
        // Debug.LogError) every time a blind random offset lands on water.
        const int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2Int target = new Vector2Int(
                Mathf.Clamp(startPos.x + Random.Range(-3, 4), 0, mapWidth - 1),
                Mathf.Clamp(startPos.y + Random.Range(-3, 4), 0, mapHeight - 1));

            if (target == startPos) continue;
            if (NativeBridge.IsWalkableForAgent(movementController.agentHandle, target.x, target.y) == 0) continue;

            if (movementController.SetNewDestination(startPos, target))
            {
                currentTargetTile = target;
                hasDestination = true;
                break;
            }
        }
        thoughtCooldown = 0.5f;
    }
}
