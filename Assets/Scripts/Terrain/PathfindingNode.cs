using UnityEngine;

public class PathfindingNode
{
    public Vector2Int GridPosition { get; private set; }
    public Vector3 WorldPosition { get; private set; }
    public BiomeType Type { get; set; }
    public float MovementWeight { get; set; }
    public bool IsWalkable { get; set; }
    public bool IsDiscovered { get; set; }

    public PathfindingNode(Vector2Int gridPos, Vector3 worldPos, BiomeType type, float weight, bool walkable)
    {
        GridPosition = gridPos;
        WorldPosition = worldPos;
        Type = type;
        MovementWeight = weight;
        IsWalkable = walkable;
        IsDiscovered = false;
    }
}