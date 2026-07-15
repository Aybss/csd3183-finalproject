using System;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleGrid : MonoBehaviour
{
    public static ObstacleGrid Instance { get; private set; }

    public event System.Action OnGridChanged;

    [Tooltip("World size of one grid cell, in meters.")]
    public float cellSize = 1.0f;

    [Tooltip("Grid origin in world space (bottom-left corner of the playable area).")]
    public Vector3 originWorldPos = Vector3.zero;

    [Tooltip("Grid dimensions in cells.")]
    public Vector2Int gridSize = new Vector2Int(100, 100);

    private bool[] _occupied;
    private bool[] _wheelchairBlocked;
    private bool[] _audioCue;
    private bool[] _visualCue;

    private Dictionary<int, GameObject> _cellToPropMap = new Dictionary<int, GameObject>();
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
        const float epsilon = 0.0001f;
        int x = Mathf.FloorToInt((local.x + epsilon) / cellSize);
        int z = Mathf.FloorToInt((local.z + epsilon) / cellSize);
        return new Vector2Int(Mathf.Clamp(x, 0, gridSize.x - 1), Mathf.Clamp(z, 0, gridSize.y - 1));
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

    public int Index(Vector2Int cell) => cell.x + cell.y * gridSize.x;

    public Vector3 GetFootprintCenterWorld(Vector2Int baseCell, Vector2Int footprint)
    {
        float width = footprint.x * cellSize;
        float length = footprint.y * cellSize;
        Vector3 minCorner = originWorldPos + new Vector3(baseCell.x * cellSize, 0f, baseCell.y * cellSize);
        return minCorner + new Vector3(width * 0.5f, 0f, length * 0.5f);
    }

    public bool CanPlaceProp(Vector2Int baseCell, Vector2Int footprintSize)
    {
        for (int dx = 0; dx < footprintSize.x; dx++)
        {
            for (int dz = 0; dz < footprintSize.y; dz++)
            {
                Vector2Int cell = new Vector2Int(baseCell.x + dx, baseCell.y + dz);
                if (!IsInBounds(cell) || _occupied[Index(cell)])
                    return false;
            }
        }
        return true;
    }

    public void RegisterProp(GameObject instance, Vector2Int baseCell, PropEntry entry)
    {
        if (!CanPlaceProp(baseCell, entry.footprintSize))
        {
            Debug.LogWarning("[ObstacleGrid] Cannot place prop here. Space is occupied or out of bounds.");
            Destroy(instance);
            return;
        }

        for (int dx = 0; dx < entry.footprintSize.x; dx++)
        {
            for (int dz = 0; dz < entry.footprintSize.y; dz++)
            {
                Vector2Int cell = new Vector2Int(baseCell.x + dx, baseCell.y + dz);
                int i = Index(cell);

                _occupied[i] = true;
                _wheelchairBlocked[i] |= entry.isWheelchairObstacle;
                _audioCue[i] |= entry.needsAudioCue;
                _visualCue[i] |= entry.needsVisualCue;

                _cellToPropMap[i] = instance;
            }
        }

        _placedProps.Add(new PlacedProp
        {
            instance = instance,
            cell = baseCell,
            footprint = entry.footprintSize
        });

        OnGridChanged?.Invoke();
    }

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

                _cellToPropMap.Remove(i);
            }
        }

        _placedProps.RemoveAt(idx);
        Destroy(instance);

        OnGridChanged?.Invoke();
    }

    public bool TryDeletePropAtWorldPos(Vector3 worldPos)
    {
        Vector2Int cell = WorldToCell(worldPos);
        if (!IsInBounds(cell)) return false;

        int i = Index(cell);
        if (_cellToPropMap.TryGetValue(i, out GameObject targetProp))
        {
            RemoveProp(targetProp);
            return true;
        }
        return false;
    }

    public bool IsCellOccupied(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return false;
        return _occupied[Index(cell)];
    }

    public byte[] ExportOccupancyBytes()
    {
        byte[] result = new byte[_occupied.Length];
        for (int i = 0; i < _occupied.Length; i++)
        {
            result[i] = (byte)(_occupied[i] ? 1 : 0);
        }
        return result;
    }
}