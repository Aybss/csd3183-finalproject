using System.Collections;
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
    [Tooltip("How many random cells to try before giving up for this cycle (in case a pick lands on a blocked/unreachable cell).")]
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

            for (int attempt = 0; attempt < maxPickAttempts && (path == null || path.Length == 0); attempt++)
            {
                Vector3 target = PickRandomWorldPosition();
                path = _agent.FindPathTo(target);
            }

            if (path != null && path.Length > 0)
                yield return StartCoroutine(FollowPath(path));
            // else: couldn't find a reachable random cell this cycle — just fall through to idle and try again next loop.

            float idleTime = Random.Range(minIdleSeconds, maxIdleSeconds);
            yield return new WaitForSeconds(idleTime);
        }
    }

    private Vector3 PickRandomWorldPosition()
    {
        int x = Random.Range(0, _gridManager.gridWidth);
        int y = Random.Range(0, _gridManager.gridHeight);
        return _gridManager.originWorldPosition + new Vector3(x * _gridManager.cellSize, 0f, y * _gridManager.cellSize);
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

            // No-op for sighted agents; lets blind agents "see" their new
            // surroundings at each step so they can replan around anything
            // they've just discovered.
            _agent.UpdateVisionAtCurrentPosition();
        }
    }
}
