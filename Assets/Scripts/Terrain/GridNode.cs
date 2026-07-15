using UnityEngine;

public enum TileType { Grass, Water, Wood, Food }

public class GridNode
{
    public Vector2Int GridPosition { get; private set; }
    public Vector3 WorldPosition { get; private set; }
    public TileType Type { get; set; }
    public float MovementWeight { get; set; }
    public bool IsWalkable { get; set; }
    public bool IsDiscovered { get; set; }

    public GridNode(Vector2Int gridPos, Vector3 worldPos, TileType type, float weight, bool walkable)
    {
        GridPosition = gridPos;
        WorldPosition = worldPos;
        Type = type;
        MovementWeight = weight;
        IsWalkable = walkable;
    }
}