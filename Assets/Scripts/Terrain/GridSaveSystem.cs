using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain
{
    // Explicit asset catalog index to track exactly what prop is sitting on a tile
    public enum PropObjectType
    {
        None = -1,
        Path = 0,
        TentA = 1,
        TentB = 2,
        StatueObelisk = 3,
        LogStack = 4,
        Bush = 5,
        Mushroom = 6,
        CropWheat = 7,
        CropBerries = 8,
        TreeLarge = 9,
        TreeSmall = 10,
        TreeTall = 11,
        PlatformGrass = 12,
        RockSmallA = 13,
        RockSmallB = 14,
        RockTallA = 15,
        RockTallB = 16,
        StoneLargeA = 17,
        StoneLargeB = 18,
        Fence = 19,
        FenceCorner = 20,
        BridgeWood = 21
    }

    [System.Serializable]
    public class SerializableNodeData
    {
        public int gridX;
        public int gridY;
        public int biomeTypeIndex;
        public float movementWeight;
        public bool isWalkable;
        public int objectTypeIndex; // Now matches PropObjectType integer cast
        public float objectRotationY; // Stores custom rotations (e.g. for fences or random foliage yaw)
    }

    [System.Serializable]
    public class GridSaveSchema
    {
        public int mapWidth;
        public int mapHeight;
        public float mapCellSize;
        public List<SerializableNodeData> serializedNodes = new List<SerializableNodeData>();
    }

    public class GridSaveSystem : MonoBehaviour
    {
        private string saveDirectoryPath;

        private void Awake()
        {
            saveDirectoryPath = Path.Combine(Application.persistentDataPath, "SavedMaps");
            if (!Directory.Exists(saveDirectoryPath))
            {
                Directory.CreateDirectory(saveDirectoryPath);
            }
        }

        // EXPORT & SAVE PIPELINE (Accepts a mapping dictionary to query live visual props)
        public void SaveMapData(string filename, PathfindingNode[,] activeGrid, Dictionary<Vector2Int, (PropObjectType type, float rotation)> activeProps, int w, int h, float size)
        {
            GridSaveSchema saveDataInstance = new GridSaveSchema
            {
                mapWidth = w,
                mapHeight = h,
                mapCellSize = size
            };

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    PathfindingNode node = activeGrid[x, y];
                    Vector2Int gridPos = new Vector2Int(x, y);

                    int objectIdx = (int)PropObjectType.None;
                    float rotY = 0f;

                    // Query our active prop registry to save objects on this tile
                    if (activeProps != null && activeProps.TryGetValue(gridPos, out var propData))
                    {
                        objectIdx = (int)propData.type;
                        rotY = propData.rotation;
                    }

                    SerializableNodeData serializedNode = new SerializableNodeData
                    {
                        gridX = node.GridPosition.x,
                        gridY = node.GridPosition.y,
                        biomeTypeIndex = (int)node.Type,
                        movementWeight = node.MovementWeight,
                        isWalkable = node.IsWalkable,
                        objectTypeIndex = objectIdx,
                        objectRotationY = rotY
                    };

                    saveDataInstance.serializedNodes.Add(serializedNode);
                }
            }

            string outJsonString = JsonUtility.ToJson(saveDataInstance, true);
            string absolutePath = Path.Combine(saveDirectoryPath, filename + ".json");

            File.WriteAllText(absolutePath, outJsonString);
            Debug.Log($"[Save System] Grid successfully exported to path: {absolutePath}");
        }

        // LOAD & IMPORT PIPELINE
        public GridSaveSchema LoadMapData(string filename)
        {
            string absolutePath = Path.Combine(saveDirectoryPath, filename + ".json");

            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[Save System] Load failed. Map file not found: {absolutePath}");
                return null;
            }

            string incomingJsonString = File.ReadAllText(absolutePath);
            GridSaveSchema loadedData = JsonUtility.FromJson<GridSaveSchema>(incomingJsonString);

            Debug.Log($"[Save System] Map file loaded successfully: {filename}.json");
            return loadedData;
        }
    }
}