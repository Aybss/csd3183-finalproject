using UnityEngine;

// Put this on ONE GameObject in your scene (e.g. an empty "GridManager").
// It owns the shared ground-truth grid that every AStarAgent plans
// against. Initialize it before any AStarAgent calls FindPathTo().
public class AStarGridManager : MonoBehaviour
{
    [Header("Grid Size")]
    public int gridWidth = 50;
    public int gridHeight = 50;

    [Header("World Mapping")]
    [Tooltip("World-space size of one grid cell. Must match the value used on your AStarAgent components.")]
    public float cellSize = 1f;

    [Tooltip("World position that corresponds to grid cell (0,0).")]
    public Vector3 originWorldPosition = Vector3.zero;

    void Awake()
    {
        NativeBridge.Init(gridWidth, gridHeight);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - originWorldPosition;
        return new Vector2Int(Mathf.RoundToInt(local.x / cellSize), Mathf.RoundToInt(local.z / cellSize));
    }

    // Marks a single cell blocked/free directly.
    public void SetBlocked(int x, int y, bool blocked)
    {
        NativeBridge.SetBlocked(x, y, blocked ? 1 : 0);
    }

    // Example helper: scans world-space colliders on the given layer and
    // marks every grid cell whose center overlaps one as blocked. Call
    // this once after Awake (or whenever your level geometry changes).
    public void BuildFromColliders(LayerMask obstacleLayer)
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Vector3 worldPos = originWorldPosition + new Vector3(x * cellSize, 0f, y * cellSize);
                bool blocked = Physics.CheckSphere(worldPos, cellSize * 0.5f, obstacleLayer);
                SetBlocked(x, y, blocked);
            }
        }
    }
}
