using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializableNodeData
{
    public int gridX;
    public int gridY;
    public int biomeTypeIndex; // Map BiomeType Enum to clean int ids
    public float movementWeight;
    public bool isWalkable;
    public int objectTypeIndex; // Track spawned props (-1 if empty)
}

[System.Serializable]
public class GridSaveSchema
{
    public int mapWidth;
    public int mapHeight;
    public float mapCellSize;
    public List<SerializableNodeData> serializedNodes = new List<SerializableNodeData>();
}