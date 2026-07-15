using UnityEngine;

public enum AgentRole
{
    WheelchairBound = 0,
    Blind = 1,
    Deaf = 2
}

[RequireComponent(typeof(Collider))]
public class AStarAgent : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The GridManager that owns the shared ground-truth grid.")]
    public AStarGridManager gridManager;

    [Header("Visuals")]
    [Tooltip("Optional LineRenderer component to visualize the path in-game.")]
    public LineRenderer pathLineRenderer;

    [Header("Agent Physical Constraints")]
    [Tooltip("Select the single role/disability that defines this agent's physical navigation capabilities.")]
    public AgentRole role = AgentRole.WheelchairBound;

    private int _agentHandle = -1;
    private const int MAX_PATH_LENGTH = 512;

    // Cache the last calculated path to draw Scene-view Gizmos
    private Vector3[] _lastCalculatedPath = new Vector3[0];

    public bool HasValidHandle() => _agentHandle >= 0;

    void Awake()
    {
        if (gridManager == null)
        {
            Debug.LogError($"{name}: AStarAgent has no GridManager assigned.", this);
            return;
        }

        // Create the C++ agent with its exclusive role definition
        _agentHandle = NativeBridge.CreateAgent((int)role);
        Debug.Log($"[{name}] Created C++ Agent ({role}) with Handle: {_agentHandle}");
    }

    // Plans a path from the agent's current position to targetWorldPos using its role capabilities
    public Vector3[] FindPathTo(Vector3 targetWorldPos)
    {
        if (_agentHandle < 0)
        {
            Debug.LogWarning($"[{name}] FindPathTo aborted: Invalid agent handle!");
            return new Vector3[0];
        }

        Vector2Int start = gridManager.WorldToGrid(transform.position);
        Vector2Int end = gridManager.WorldToGrid(targetWorldPos);

        Debug.Log($"[{name}] Requesting path. World Start: {transform.position} -> Grid Start: {start} | World End: {targetWorldPos} -> Grid End: {end}");

        var gridPath = NativeBridge.FindAgentPath(_agentHandle, start.x, start.y, end.x, end.y, MAX_PATH_LENGTH);

        if (gridPath == null || gridPath.Count == 0)
        {
            Debug.LogWarning($"[{name}] DLL returned null or empty path!");
            ClearPathVisuals();
            return new Vector3[0];
        }

        Debug.Log($"[{name}] Path successfully found! Waypoints: {gridPath.Count}");

        Vector3[] path = new Vector3[gridPath.Count];
        for (int i = 0; i < gridPath.Count; i++)
        {
            // Center the path waypoints slightly on the Y-axis so they don't clip into the floor plane
            path[i] = gridManager.originWorldPosition
                      + new Vector3(gridPath[i].x * gridManager.cellSize, 0.2f, gridPath[i].y * gridManager.cellSize);
        }

        // Store and update visuals
        _lastCalculatedPath = path;
        UpdateLineRendererVisuals(path);

        return path;
    }

    private void UpdateLineRendererVisuals(Vector3[] path)
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.positionCount = path.Length;
            pathLineRenderer.SetPositions(path);
        }
    }

    public void ClearPathVisuals()
    {
        _lastCalculatedPath = new Vector3[0];
        if (pathLineRenderer != null)
        {
            pathLineRenderer.positionCount = 0;
        }
    }

    private void OnDrawGizmos()
    {
        if (_lastCalculatedPath == null || _lastCalculatedPath.Length < 2) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < _lastCalculatedPath.Length - 1; i++)
        {
            Gizmos.DrawLine(_lastCalculatedPath[i], _lastCalculatedPath[i + 1]);
            Gizmos.DrawSphere(_lastCalculatedPath[i], 0.15f); // Draw waypoint nodes
        }
    }

    private void OnDestroy()
    {
        if (_agentHandle >= 0)
        {
            Debug.Log($"[{name}] Releasing native agent resources (Handle: {_agentHandle})...");
            NativeBridge.DestroyAgent(_agentHandle);
            _agentHandle = -1;
        }
    }
}