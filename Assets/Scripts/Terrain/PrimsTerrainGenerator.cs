using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain
{
    public class PrimsTerrainGenerator : MonoBehaviour
    {
        [Header("Grid Scale Settings")]
        public int width = 50;
        public int height = 50;
        public float cellSize = 1f;

        [Header("Continuous Meandering River Settings")]
        [Tooltip("Lower values create smooth, long-winding bends.")]
        public float riverMeanderFrequency = 0.05f;
        [Tooltip("Maximum distance the river center can drift.")]
        public float riverMaxMeanderAmplitude = 12f;
        [Tooltip("The exact thickness of your river in grid cells.")]
        [Range(1, 5)] public int riverTileThickness = 1;

        [Header("Kenney Asset Alignment Helper")]
        [Tooltip("Nudges all calculated river rotations in steps of 90 degrees.")]
        [Range(0, 3)] public int riverRotationOffsetSteps = 0;

        [Header("Infrastructure")]
        public int minBridgeSpacing = 8;

        public KenneyTileConfiguration tilePack;
        public GridSaveSystem saveSystem;

        private PathfindingNode[,] grid;
        private HashSet<Vector2Int> spawnedBridgeCoordinates = new HashSet<Vector2Int>();
        private float seedX;

        // Keep track of spawned objects so they can be cleaned up on reload
        private List<GameObject> spawnedVisualObjects = new List<GameObject>();

        private void Start()
        {
            seedX = Random.Range(0f, 5000f);

            // Default run: generate a brand new procedural map
            GenerateNewMap();
        }

        public void GenerateNewMap()
        {
            ClearActiveMap();
            GenerateNaturalPlains();
            ConstructVisualGrid();
        }

        // RECONSTRUCT SCENE FROM JSON SCHEMA
        public void ReconstructMapFromSchema(GridSaveSchema schema)
        {
            if (schema == null) return;

            ClearActiveMap();
            width = schema.mapWidth;
            height = schema.mapHeight;
            cellSize = schema.mapCellSize;

            grid = new PathfindingNode[width, height];

            // Rebuild Node Data Layer
            foreach (var nodeData in schema.serializedNodes)
            {
                int x = nodeData.gridX;
                int y = nodeData.gridY;
                Vector3 worldPos = new Vector3(x * cellSize, 0f, y * cellSize);

                BiomeType type = (BiomeType)nodeData.biomeTypeIndex;
                grid[x, y] = new PathfindingNode(new Vector2Int(x, y), worldPos, type, nodeData.movementWeight, nodeData.isWalkable);
            }

            // Rebuild Visual Layer
            ConstructVisualGrid(schema);
        }

        private void ClearActiveMap()
        {
            foreach (var obj in spawnedVisualObjects)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedVisualObjects.Clear();
            spawnedBridgeCoordinates.Clear();
        }

        private void GenerateNaturalPlains()
        {
            grid = new PathfindingNode[width, height];
            HashSet<Vector2Int> riverCoordinates = CalculatePathWalkedRiver();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Vector3 worldPos = new Vector3(x * cellSize, 0f, y * cellSize);

                    BiomeType assignedType = BiomeType.Grass;

                    if (riverCoordinates.Contains(pos))
                    {
                        assignedType = BiomeType.Water;
                    }

                    float weight = (assignedType == BiomeType.Water) ? 5.0f : 1.0f;
                    grid[x, y] = new PathfindingNode(pos, worldPos, assignedType, weight, true);
                }
            }
        }

        private HashSet<Vector2Int> CalculatePathWalkedRiver()
        {
            HashSet<Vector2Int> riverPoints = new HashSet<Vector2Int>();
            float gridCenterColumn = width / 2f;

            for (int y = 0; y < height; y++)
            {
                float noiseSample = Mathf.PerlinNoise((y * riverMeanderFrequency) + seedX, 0f);
                float normalizedDrift = (noiseSample - 0.5f) * 2f;

                int riverCenterTargetX = Mathf.RoundToInt(gridCenterColumn + (normalizedDrift * riverMaxMeanderAmplitude));

                int halfThickness = riverTileThickness / 2;
                for (int offset = -halfThickness; offset <= halfThickness; offset++)
                {
                    int targetX = riverCenterTargetX + offset;
                    if (targetX >= 0 && targetX < width)
                    {
                        riverPoints.Add(new Vector2Int(targetX, y));
                    }
                }
            }
            return riverPoints;
        }

        private void ConstructVisualGrid(GridSaveSchema loadedSchema = null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    PathfindingNode node = grid[x, y];
                    Vector3 basePos = node.WorldPosition;

                    if (node.Type != BiomeType.Water)
                    {
                        SpawnVisual(tilePack.grassCenter, basePos, Quaternion.identity);

                        if (loadedSchema != null)
                        {
                            // Rebuild saved visual props directly using saved indices
                            SerializableNodeData savedNode = loadedSchema.serializedNodes.Find(n => n.gridX == x && n.gridY == y);
                            if (savedNode != null && savedNode.objectTypeIndex != (int)PropObjectType.None)
                            {
                                ReconstructSavedProp((PropObjectType)savedNode.objectTypeIndex, basePos, savedNode.objectRotationY);
                            }
                        }
                        else
                        {
                            EvaluateAndSpawnStandardProps(x, y, basePos);
                        }
                        continue;
                    }

                    // Water Neighbors 
                    bool landN = IsLand(x, y + 1);
                    bool landNE = IsLand(x + 1, y + 1);
                    bool landE = IsLand(x + 1, y);
                    bool landSE = IsLand(x + 1, y - 1);
                    bool landS = IsLand(x, y - 1);
                    bool landSW = IsLand(x - 1, y - 1);
                    bool landW = IsLand(x - 1, y);
                    bool landNW = IsLand(x - 1, y + 1);

                    // Default to solid water center prefab
                    GameObject selectedTile = tilePack.waterCenter.prefab;
                    float baseRotation = 0f;
                    float appliedRiverYOffset = tilePack.waterCenter.yOffset;

                    // Straight shores
                    if (landN && !landS && !landE && !landW) { selectedTile = tilePack.ground_riverSide.prefab; baseRotation = 0f; appliedRiverYOffset = tilePack.ground_riverSide.yOffset; }
                    else if (landE && !landW && !landN && !landS) { selectedTile = tilePack.ground_riverSide.prefab; baseRotation = 90f; appliedRiverYOffset = tilePack.ground_riverSide.yOffset; }
                    else if (landS && !landN && !landE && !landW) { selectedTile = tilePack.ground_riverSide.prefab; baseRotation = 180f; appliedRiverYOffset = tilePack.ground_riverSide.yOffset; }
                    else if (landW && !landE && !landN && !landS) { selectedTile = tilePack.ground_riverSide.prefab; baseRotation = 270f; appliedRiverYOffset = tilePack.ground_riverSide.yOffset; }

                    // Corner shores
                    else if (landN && landE && !landS && !landW) { selectedTile = tilePack.ground_riverCorner.prefab; baseRotation = 0f; appliedRiverYOffset = tilePack.ground_riverCorner.yOffset; }
                    else if (landE && landS && !landN && !landW) { selectedTile = tilePack.ground_riverCorner.prefab; baseRotation = 90f; appliedRiverYOffset = tilePack.ground_riverCorner.yOffset; }
                    else if (landS && landW && !landN && !landE) { selectedTile = tilePack.ground_riverCorner.prefab; baseRotation = 180f; appliedRiverYOffset = tilePack.ground_riverCorner.yOffset; }
                    else if (landW && landN && !landE && !landS) { selectedTile = tilePack.ground_riverCorner.prefab; baseRotation = 270f; appliedRiverYOffset = tilePack.ground_riverCorner.yOffset; }

                    // Open/Inner Corner shores
                    else if (!landN && !landE && !landS && !landW)
                    {
                        if (landNE) { selectedTile = tilePack.ground_riverSideOpen.prefab; baseRotation = 0f; appliedRiverYOffset = tilePack.ground_riverSideOpen.yOffset; }
                        else if (landSE) { selectedTile = tilePack.ground_riverSideOpen.prefab; baseRotation = 90f; appliedRiverYOffset = tilePack.ground_riverSideOpen.yOffset; }
                        else if (landSW) { selectedTile = tilePack.ground_riverSideOpen.prefab; baseRotation = 180f; appliedRiverYOffset = tilePack.ground_riverSideOpen.yOffset; }
                        else if (landNW) { selectedTile = tilePack.ground_riverSideOpen.prefab; baseRotation = 270f; appliedRiverYOffset = tilePack.ground_riverSideOpen.yOffset; }
                    }

                    // Fallback configuration: if no shore piece was selected, guarantee a water center
                    if (selectedTile == null)
                    {
                        selectedTile = tilePack.waterCenter.prefab;
                        appliedRiverYOffset = tilePack.waterCenter.yOffset;
                        baseRotation = 0f;
                    }

                    Vector3 riverSpawnPos = new Vector3(basePos.x, basePos.y + appliedRiverYOffset, basePos.z);
                    float finalRotation = (baseRotation + (riverRotationOffsetSteps * 90f)) % 360f;
                    SpawnVisual(selectedTile, riverSpawnPos, Quaternion.Euler(0, finalRotation, 0));

                    // Build Saved Bridge
                    if (loadedSchema != null)
                    {
                        SerializableNodeData savedNode = loadedSchema.serializedNodes.Find(n => n.gridX == x && n.gridY == y);
                        if (savedNode != null && savedNode.objectTypeIndex == (int)PropObjectType.BridgeWood)
                        {
                            Vector3 bridgePos = new Vector3(basePos.x, basePos.y + tilePack.bridge_wood.yOffset, basePos.z);
                            SpawnVisual(tilePack.bridge_wood.prefab, bridgePos, Quaternion.Euler(0, savedNode.objectRotationY, 0));
                        }
                    }
                    else
                    {
                        bool waterN = !landN; bool waterS = !landS; bool waterE = !landE; bool waterW = !landW;
                        if (((!waterN && !waterS) && waterE && waterW) || ((!waterE && !waterW) && waterN && waterS))
                        {
                            TryPlaceSmartBridge(x, y, basePos, (waterE && waterW));
                        }
                    }
                }
            }
        }

        private void ReconstructSavedProp(PropObjectType type, Vector3 basePos, float rotationY)
        {
            SpawnConfiguration config = GetConfigFromType(type);
            if (config.prefab != null)
            {
                Vector3 spawnPos = new Vector3(basePos.x, basePos.y + config.yOffset, basePos.z);
                SpawnVisual(config.prefab, spawnPos, Quaternion.Euler(0, rotationY, 0));
            }
        }

        private SpawnConfiguration GetConfigFromType(PropObjectType type)
        {
            switch (type)
            {
                case PropObjectType.Path: return tilePack.path;
                case PropObjectType.TentA: return tilePack.tent_a;
                case PropObjectType.TentB: return tilePack.tent_b;
                case PropObjectType.StatueObelisk: return tilePack.statue_obelisk;
                case PropObjectType.LogStack: return tilePack.log_stack;
                case PropObjectType.Bush: return tilePack.bush;
                case PropObjectType.Mushroom: return tilePack.mushroom;
                case PropObjectType.CropWheat: return tilePack.crop_wheat;
                case PropObjectType.CropBerries: return tilePack.crop_berries;
                case PropObjectType.TreeLarge: return tilePack.tree_large;
                case PropObjectType.TreeSmall: return tilePack.tree_small;
                case PropObjectType.TreeTall: return tilePack.tree_tall;
                case PropObjectType.PlatformGrass: return tilePack.platform_grass;
                case PropObjectType.RockSmallA: return tilePack.rock_small_a;
                case PropObjectType.RockSmallB: return tilePack.rock_small_b;
                case PropObjectType.RockTallA: return tilePack.rock_tall_a;
                case PropObjectType.RockTallB: return tilePack.rock_tall_b;
                case PropObjectType.StoneLargeA: return tilePack.stone_large_a;
                case PropObjectType.StoneLargeB: return tilePack.stone_large_b;
                case PropObjectType.Fence: return tilePack.fence;
                case PropObjectType.FenceCorner: return tilePack.fence_corner;
                case PropObjectType.BridgeWood: return tilePack.bridge_wood;
                default: return default;
            }
        }

        private void EvaluateAndSpawnStandardProps(int x, int y, Vector3 basePos)
        {
            List<(SpawnConfiguration config, bool useRandRot)> spawnList = new List<(SpawnConfiguration, bool)>()
            {
                (tilePack.path, false),
                (tilePack.tent_a, true), (tilePack.tent_b, true),
                (tilePack.statue_obelisk, false), (tilePack.log_stack, true),
                (tilePack.bush, true), (tilePack.mushroom, true),
                (tilePack.crop_wheat, false), (tilePack.crop_berries, false),
                (tilePack.tree_large, true), (tilePack.tree_small, true), (tilePack.tree_tall, true),
                (tilePack.platform_grass, false),
                (tilePack.rock_small_a, true), (tilePack.rock_small_b, true),
                (tilePack.rock_tall_a, true), (tilePack.rock_tall_b, true),
                (tilePack.stone_large_a, true), (tilePack.stone_large_b, true)
            };

            foreach (var item in spawnList)
            {
                if (item.config.prefab != null && Random.value < item.config.density)
                {
                    Vector3 spawnPosition = new Vector3(basePos.x, basePos.y + item.config.yOffset, basePos.z);
                    float yRot = item.useRandRot ? Random.Range(0f, 360f) : 0f;
                    SpawnVisual(item.config.prefab, spawnPosition, Quaternion.Euler(0, yRot, 0));
                    return;
                }
            }
        }

        private void TryPlaceSmartBridge(int x, int y, Vector3 pos, bool isHorizontal)
        {
            if (tilePack.bridge_wood.prefab == null || Random.value > tilePack.bridge_wood.density) return;

            foreach (Vector2Int bridgeCoord in spawnedBridgeCoordinates)
            {
                if (Vector2Int.Distance(new Vector2Int(x, y), bridgeCoord) < minBridgeSpacing)
                    return;
            }

            float rotationY = isHorizontal ? 90f : 0f;
            Vector3 bridgePos = new Vector3(pos.x, pos.y + tilePack.bridge_wood.yOffset, pos.z);

            SpawnVisual(tilePack.bridge_wood.prefab, bridgePos, Quaternion.Euler(0, rotationY, 0));
            spawnedBridgeCoordinates.Add(new Vector2Int(x, y));
        }

        private void SpawnVisual(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return;
            GameObject inst = Instantiate(prefab, pos, rot, transform);
            spawnedVisualObjects.Add(inst);
        }

        private bool IsLand(int x, int y)
        {
            if (!IsInsideBounds(x, y)) return false;
            return grid[x, y].Type != BiomeType.Water;
        }

        private bool IsInsideBounds(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }
    }

    // Returns a 2D array of lightweight structs ready for pathfinding
    public SimpleNodeData[,] ExportLightweight2DArray()
        {
            SimpleNodeData[,] dataMatrix = new SimpleNodeData[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    PathfindingNode sourceNode = grid[x, y];

                    dataMatrix[x, y] = new SimpleNodeData
                    {
                        x = x,
                        y = y,
                        isWalkable = sourceNode.IsWalkable,
                        movementCost = sourceNode.MovementWeight,
                        biomeType = (int)sourceNode.Type
                    };
                }
            }

            return dataMatrix;
        }
    }