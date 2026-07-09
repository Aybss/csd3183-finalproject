using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which world-space grid cells are occupied by placed props,
/// along with their accessibility flags. This is the data structure
/// that will eventually be flattened and passed into the C++
/// PathfinderCore plugin (occupancy grid + hazard flags per cell).
///
/// For now it just needs to exist so the placement tool has
/// somewhere to record what got dropped where.
/// </summary>
public class ObstacleGrid : MonoBehaviour
{
    public static ObstacleGrid Instance { get; private set; }

    [Tooltip("World size of one grid cell, in meters.")]
    public float cellSize = 1.0f;

    [Tooltip("Grid origin in world space (bottom-left corner of the playable area).")]
    public Vector3 originWorldPos = Vector3.zero;

    [Tooltip("Grid dimensions in cells.")]
    public Vector2Int gridSize = new Vector2Int(100, 100);

    // Per-cell occupancy + flags. Flat array indexed as x + z * gridSize.x
    private bool[] _occupied;
    private bool[] _wheelchairBlocked;
    private bool[] _audioCue;
    private bool[] _visualCue;

    private readonly List<PlacedProp> _placedProps = new List<PlacedProp>();

    private struct PlacedProp
    {
        public GameObject instance;
        public Vector2Int cell;
        public Vector2Int footprint;
    }

    private void Awake()
    {
        Instance = this;
        int count = gridSize.x * gridSize.y;
        _occupied = new bool[count];
        _wheelchairBlocked = new bool[count];
        _audioCue = new bool[count];
        _visualCue = new bool[count];
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - originWorldPos;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return originWorldPos + new Vector3(
            (cell.x + 0.5f) * cellSize,
            0f,
            (cell.y + 0.5f) * cellSize);
    }

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;
    }

    private int Index(Vector2Int cell) => cell.x + cell.y * gridSize.x;

    /// <summary>Registers a placed prop's footprint into the grid.</summary>
    public void RegisterProp(GameObject instance, Vector3 worldPos, PropEntry entry)
    {
        Vector2Int baseCell = WorldToCell(worldPos);

        for (int dx = 0; dx < entry.footprintSize.x; dx++)
        {
            for (int dz = 0; dz < entry.footprintSize.y; dz++)
            {
                Vector2Int cell = new Vector2Int(baseCell.x + dx, baseCell.y + dz);
                if (!IsInBounds(cell)) continue;

                int i = Index(cell);
                _occupied[i] = true;
                _wheelchairBlocked[i] |= entry.isWheelchairObstacle;
                _audioCue[i] |= entry.needsAudioCue;
                _visualCue[i] |= entry.needsVisualCue;
            }
        }

        _placedProps.Add(new PlacedProp
        {
            instance = instance,
            cell = baseCell,
            footprint = entry.footprintSize
        });
    }

    /// <summary>Removes a previously placed prop (e.g. for an undo action).</summary>
    public void RemoveProp(GameObject instance)
    {
        int idx = _placedProps.FindIndex(p => p.instance == instance);
        if (idx < 0) return;

        var p = _placedProps[idx];
        for (int dx = 0; dx < p.footprint.x; dx++)
        {
            for (int dz = 0; dz < p.footprint.y; dz++)
            {
                Vector2Int cell = new Vector2Int(p.cell.x + dx, p.cell.y + dz);
                if (!IsInBounds(cell)) continue;

                int i = Index(cell);
                _occupied[i] = false;
                _wheelchairBlocked[i] = false;
                _audioCue[i] = false;
                _visualCue[i] = false;
            }
        }

        _placedProps.RemoveAt(idx);
    }

    /// <summary>
    /// Flattens the occupancy layer into a byte array suitable for
    /// marshaling to the C++ plugin later (0 = free, 1 = blocked).
    /// </summary>
    public byte[] ExportOccupancyBytes()
    {
        byte[] result = new byte[_occupied.Length];
        for (int i = 0; i < _occupied.Length; i++)
            result[i] = (byte)(_occupied[i] ? 1 : 0);
        return result;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Vector3 size = new Vector3(gridSize.x * cellSize, 0.01f, gridSize.y * cellSize);
        Gizmos.DrawCube(originWorldPos + size * 0.5f, size);
    }
}
