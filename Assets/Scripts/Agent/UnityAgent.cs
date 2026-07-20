using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class UnityAgent : MonoBehaviour
{
    public int agentHandle = -1;
    public int role = 0;
    public float movementSpeed = 5f;
    public float cellSize = 1f;

    // Wheelchair users are visibly slower over ground than the other roles —
    // mirrors the extra pathing cost RoleCellCostMultiplier gives them on
    // bridges natively, but this affects every tile, not just bridges.
    private static readonly float[] RoleSpeedMultiplier = { 0.6f, 1f, 1f }; // WheelchairBound, Blind, Deaf
    private float effectiveMovementSpeed = 5f; // overwritten by Initialize(); this default only matters if Initialize is never called

    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int currentPathIndex = 0;
    private Vector2Int lastGridPosition = new Vector2Int(-1, -1);

    private Rigidbody rb;
    private AgentStats stats;

    // Cached so we can defensively reject/clamp any waypoint that falls outside
    // the actual map, regardless of what the native pathfinder hands back.
    private int mapWidth = -1;
    private int mapHeight = -1;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        stats = GetComponent<AgentStats>();

        var generator = GameObject.FindObjectOfType<ProceduralTerrain.PrimsTerrainGenerator>();
        if (generator != null)
        {
            mapWidth = generator.width;
            mapHeight = generator.height;
        }

        SnapToGround();

        // Movement is fully authored via MovePosition/MoveRotation in FixedUpdate.
        // A non-kinematic body still responds to real physics impulses (e.g. several
        // agents overlapping at the same resource/build tile), and MovePosition never
        // cancels that residual velocity � with zero drag it never decays, so one shove
        // sends the agent drifting in a straight line forever. Kinematic removes that
        // failure mode entirely while still letting other colliders detect it.
        rb.isKinematic = true;
        // Belt-and-suspenders: lock the Y axis outright so nothing (a future
        // change to isKinematic, a stray force, another script) can ever
        // move the agent vertically.
        rb.constraints = RigidbodyConstraints.FreezePositionY;
    }

    private void SnapToGround()
    {
        // Collider/ground-height math kept getting this wrong for this
        // prefab's actual pivot/collider setup. Flat, simple, and correct in
        // practice: spawn position plus a fixed clearance.
        Vector3 pos = transform.position;
        pos.y += 0.1f;
        transform.position = pos;
    }

    public void Initialize(int handle, int roleType, float size)
    {
        agentHandle = handle;
        role = roleType;
        cellSize = size;

        float multiplier = (roleType >= 0 && roleType < RoleSpeedMultiplier.Length) ? RoleSpeedMultiplier[roleType] : 1f;
        effectiveMovementSpeed = movementSpeed * multiplier;

        ImpairmentVisuals.Apply(gameObject, (AgentRoleType)roleType, size);
    }

    public bool SetNewDestination(Vector2Int start, Vector2Int destination)
    {
        if (!IsInMapBounds(destination))
        {
            Debug.LogError($"[UnityAgent] Rejected destination {destination} � outside the {mapWidth}x{mapHeight} grid.");
            return false;
        }

        List<Vector2Int> path = NativeBridge.FindAgentPath(agentHandle, start.x, start.y, destination.x, destination.y);

        if (path == null || path.Count == 0)
        {
            // Not a bug: a wander target or a stale resource tile can easily be
            // unreachable (river with no nearby bridge, rubble-boxed pocket for
            // a wheelchair-bound agent, etc). Every caller already checks this
            // return value and retries/falls back — a warning is enough, and
            // keeps the Editor's "Error Pause" from halting a normal outcome.
            Debug.LogWarning($"[UnityAgent] No path from {start} to {destination} — will retry.");
            return false;
        }

        // Defensive net: even though the native grid should never hand back an
        // out-of-bounds node, don't let a stray coordinate walk the agent off
        // the visible map if it ever does.
        foreach (var node in path)
        {
            if (!IsInMapBounds(node))
            {
                Debug.LogError($"[UnityAgent] Native path contained out-of-bounds node {node} � discarding path.");
                return false;
            }
        }

        currentPath = path;
        currentPathIndex = 0;
        return true;
    }

    private bool IsInMapBounds(Vector2Int pos)
    {
        if (mapWidth < 0 || mapHeight < 0) return true; // haven't found the generator yet, don't block movement
        return pos.x >= 0 && pos.x < mapWidth && pos.y >= 0 && pos.y < mapHeight;
    }

    private void FixedUpdate()
    {
        if (stats != null && stats.isDead) return; // freeze in place — AgentDeathVisual shows the cross-out
        if (currentPath == null || currentPathIndex >= currentPath.Count) return;

        Vector2Int targetGridPos = currentPath[currentPathIndex];
        Vector3 targetWorldPos = new Vector3(targetGridPos.x * cellSize, rb.position.y, targetGridPos.y * cellSize);

        // Move via Rigidbody so gravity and collisions actually apply � this is
        // what keeps the agent on solid ground and stops it at obstacles/other agents.
        Vector3 newPos = Vector3.MoveTowards(rb.position, targetWorldPos, effectiveMovementSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        int currentX = Mathf.RoundToInt(rb.position.x / cellSize);
        int currentY = Mathf.RoundToInt(rb.position.z / cellSize);
        Vector2Int currentGridPos = new Vector2Int(currentX, currentY);

        if (currentGridPos != lastGridPosition)
        {
            if (agentHandle >= 0)
            {
                // SLAM: clears fog-of-war around the agent's new tile (sight
                // radius from its role's AgentProfile, plus hearing range for
                // food) and records it in that agent's own AgentMemory.
                NativeBridge.AgentPerceive(agentHandle, currentX, currentY);
            }
            lastGridPosition = currentGridPos;
        }

        Vector3 direction = targetWorldPos - rb.position;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 10f));
        }

        Vector2 flatAgentPos = new Vector2(rb.position.x, rb.position.z);
        Vector2 flatTargetPos = new Vector2(targetWorldPos.x, targetWorldPos.z);
        if (Vector2.Distance(flatAgentPos, flatTargetPos) < 0.2f)
        {
            currentPathIndex++;
        }
    }

    private void OnDestroy()
    {
        if (agentHandle >= 0)
        {
            NativeBridge.DestroyAgent(agentHandle);
        }
    }
}