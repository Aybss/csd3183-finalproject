using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach alongside AStarAgent on the same GameObject. Continuously
// picks a random reachable cell on the grid, walks the path there,
// idles for a random duration, then picks a new one.
[RequireComponent(typeof(AStarAgent))]
public class NPCWanderer : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    [Tooltip("How close (world units) counts as 'arrived' at a waypoint.")]
    public float waypointReachedDistance = 0.05f;

    [Header("Idle")]
    public float minIdleSeconds = 2f;
    public float maxIdleSeconds = 5f;

    [Header("Wander")]
    [Tooltip("How many random positions to evaluate in a batch.")]
    public int candidateBatchSize = 15;
    [Tooltip("How many pathfinding attempts to make if the first choices are blocked.")]
    public int maxPickAttempts = 10;

    private AStarAgent _agent;
    private AStarGridManager _gridManager;

    void Start()
    {
        _agent = GetComponent<AStarAgent>();
        _gridManager = _agent.gridManager;

        if (_gridManager == null)
        {
            Debug.LogError($"{name}: NPCWanderer needs its AStarAgent to have a GridManager assigned.", this);
            return;
        }

        StartCoroutine(WanderLoop());
    }

    private IEnumerator WanderLoop()
    {
        while (_agent == null || !_agent.HasValidHandle())
        {
            yield return null;
        }

        while (true)
        {
            Vector3[] path = null;

            // 1. Gather a batch of random coordinates
            List<Vector2Int> candidates = GatherCandidates();

            // 2. Attempt to find a path to the best candidates first
            for (int i = 0; i < candidates.Count && i < maxPickAttempts; i++)
            {
                Vector3 targetWorld = GridToWorld(candidates[i]);
                path = _agent.FindPathTo(targetWorld);

                if (path != null && path.Length > 0)
                {
                    break; // Successfully found a path!
                }
            }

            // 3. Follow path or idle
            if (path != null && path.Length > 0)
            {
                yield return StartCoroutine(FollowPath(path));
            }
            else
            {
                Debug.LogWarning($"[{name}] Wanderer could not find a reachable destination from candidate batch. Retrying...");
            }

            float idleTime = Random.Range(minIdleSeconds, maxIdleSeconds);
            yield return new WaitForSeconds(idleTime);
        }
    }

    private List<Vector2Int> GatherCandidates()
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int i = 0; i < candidateBatchSize; i++)
        {
            int x = Random.Range(0, _gridManager.gridWidth);
            int y = Random.Range(0, _gridManager.gridHeight);
            candidates.Add(new Vector2Int(x, y));
        }

        // Shuffle the list so we wander randomly
        Shuffle(candidates);

        return candidates;
    }

    private Vector3 GridToWorld(Vector2Int gridCoord)
    {
        return _gridManager.originWorldPosition
               + new Vector3(gridCoord.x * _gridManager.cellSize, 0f, gridCoord.y * _gridManager.cellSize);
    }

    private Vector3 GridToWorld(Vector2Int gridCoord, float heightOffset = 0f)
    {
        return _gridManager.originWorldPosition
               + new Vector3(gridCoord.x * _gridManager.cellSize, heightOffset, gridCoord.y * _gridManager.cellSize);
    }

    private IEnumerator FollowPath(Vector3[] path)
    {
        foreach (var waypoint in path)
        {
            while (Vector3.Distance(transform.position, waypoint) > waypointReachedDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, waypoint, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }
}