using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ProceduralTerrain
{
    // --- LIGHTWEIGHT EXPORT STRUCT ---
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [System.Serializable]
    public struct SimpleNodeData
    {
        public int x;
        public int y;
        [MarshalAs(UnmanagedType.I1)] public bool isWalkable;
        public float movementCost;
        public int biomeType;
    }

    public class PrimsTerrainGenerator : MonoBehaviour
    {
        [Header("Grid Scale Settings")]
        public int width = 50;
        public int height = 50;
        public float cellSize = 1f;

        [Header("Continuous Meandering River Settings")]
        [Tooltip("Lower values create smooth, long-winding bends.")]
        public float riverMeanderFrequency = 0.05f;
        [Tooltip("Maximum distance a river segment can drift from the straight line between its two endpoints.")]
        public float riverMaxMeanderAmplitude = 12f;
        [Tooltip("Rivers are randomly 1 to this many tiles wide, per segment.")]
        [Range(1, 5)] public int riverTileThickness = 2;

        [Header("Prim's Algorithm Water Network")]
        [Tooltip("Water-network seed points connected by a genuine Prim's-algorithm MST (PathNetwork.cpp) — randomizes the river layout every generation while keeping it a single connected network instead of scattered disconnected lakes.")]
        [Range(2, 8)] public int waterSeedCount = 4;

        [Header("Random Small Water Bodies")]
        [Tooltip("Number of random small ponds to seed across the plains.")]
        public int pondCount = 5;
        [Tooltip("Maximum size parameter for seeded ponds.")]
        [Range(1, 4)] public int maxPondRadius = 2;

        [Header("Kenney Asset Alignment Helper")]
        [Tooltip("Nudges all calculated river rotations in steps of 90 degrees.")]
        [Range(0, 3)] public int riverRotationOffsetSteps = 0;

        [Header("Infrastructure & Bridge Traversal")]
        public int minBridgeSpacing = 8;
        [Tooltip("Force a bridge crossing every N steps along each river segment's length, to guarantee pathfinding connection without relying solely on EnsureFullMapConnectivity's repair pass.")]
        public int forcedBridgeInterval = 12;

        public KenneyTileConfiguration tilePack;
        public GridSaveSystem saveSystem;

        private PathfindingNode[,] grid;
        private HashSet<Vector2Int> spawnedBridgeCoordinates = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> forcedBridgeCoordinates = new HashSet<Vector2Int>();
        // true = bridge model spans east-west (crossing a north/south-flowing
        // segment); false = spans north-south. Needed now that river segments
        // run in arbitrary directions instead of always top-to-bottom.
        private Dictionary<Vector2Int, bool> forcedBridgeOrientation = new Dictionary<Vector2Int, bool>();

        // Keep track of spawned objects so they can be cleaned up on reload
        private List<GameObject> spawnedVisualObjects = new List<GameObject>();

        private void Start()
        {
            // Default run: generate a brand new procedural map
            GenerateNewMap();
        }

        public void GenerateNewMap()
        {
            ClearActiveMap();
            GenerateNaturalPlains();
            ConstructVisualGrid();
            MarkRubbleAroundStoneDeposits();

            // Generate the physical collider bounds of the map once built
            CreateGlobalFloorCollider();
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

            // Rebuild physical collider state
            CreateGlobalFloorCollider();
        }

        private void ClearActiveMap()
        {
            foreach (var obj in spawnedVisualObjects)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedVisualObjects.Clear();
            spawnedBridgeCoordinates.Clear();
            forcedBridgeCoordinates.Clear();
            forcedBridgeOrientation.Clear();
        }

        private void GenerateNaturalPlains()
        {
            grid = new PathfindingNode[width, height];
            HashSet<Vector2Int> riverCoordinates = GenerateWaterNetwork();
            HashSet<Vector2Int> pondCoordinates = CalculateRandomPonds(riverCoordinates);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Vector3 worldPos = new Vector3(x * cellSize, 0f, y * cellSize);

                    BiomeType assignedType = BiomeType.Grass;
                    bool walkable = true;
                    float weight = 1.0f;

                    if (riverCoordinates.Contains(pos) || pondCoordinates.Contains(pos))
                    {
                        assignedType = BiomeType.Water;
                        walkable = false; // Water blocks movement by default
                        weight = 5.0f;
                    }

                    // FORCE WALKABILITY FOR PATHFINDING ON BRIDGE TILES
                    if (forcedBridgeCoordinates.Contains(pos))
                    {
                        assignedType = BiomeType.Water; // Visually water underneath
                        walkable = true; // Pathfinding can cross here!
                        weight = 1.0f; // Low cost to traverse bridge
                    }

                    grid[x, y] = new PathfindingNode(pos, worldPos, assignedType, weight, walkable);
                }
            }

            // Hard backstop: guarantee every walkable tile is reachable from
            // every other walkable tile, regardless of how the randomized
            // water network above happened to lay out.
            EnsureFullMapConnectivity();
        }

        // Replaces the old single hardcoded top-to-bottom river. Scatters a
        // handful of seed points, connects them with a genuine Prim's-algorithm
        // minimum spanning tree (NativeBridge.GeneratePathNetwork ->
        // PathNetwork.cpp's ComputePrimsMST — frontier/cheapest-edge, not a
        // DFS maze carver or raw Perlin noise), then carves a meandering,
        // randomly-thick water segment along each MST edge. Randomized layout
        // every generation, but always a single connected network — "random
        // but controlled."
        private HashSet<Vector2Int> GenerateWaterNetwork()
        {
            HashSet<Vector2Int> waterPoints = new HashSet<Vector2Int>();

            List<Vector2Int> seeds = PickScatteredWaterSeeds(waterSeedCount);
            if (seeds.Count < 2) return waterPoints; // map too small for a network

            List<(int fromIndex, int toIndex)> mstEdges = NativeBridge.GeneratePathNetwork(seeds);

            foreach (var edge in mstEdges)
            {
                CarveMeanderingRiverSegment(seeds[edge.fromIndex], seeds[edge.toIndex], waterPoints);
            }

            return waterPoints;
        }

        private List<Vector2Int> PickScatteredWaterSeeds(int count)
        {
            List<Vector2Int> seeds = new List<Vector2Int>();
            int minSeparation = Mathf.Max(6, Mathf.Min(width, height) / 4);
            int attempts = 0;

            while (seeds.Count < count && attempts < count * 40)
            {
                attempts++;
                Vector2Int candidate = new Vector2Int(Random.Range(4, width - 4), Random.Range(4, height - 4));

                bool tooClose = false;
                foreach (Vector2Int existing in seeds)
                {
                    if (Vector2Int.Distance(existing, candidate) < minSeparation) { tooClose = true; break; }
                }
                if (!tooClose) seeds.Add(candidate);
            }
            return seeds;
        }

        private void CarveMeanderingRiverSegment(Vector2Int from, Vector2Int to, HashSet<Vector2Int> waterPoints)
        {
            int thickness = Random.Range(1, riverTileThickness + 1);
            float localSeed = Random.Range(0f, 5000f);

            Vector2 dir = new Vector2(to.x - from.x, to.y - from.y);
            float length = dir.magnitude;
            if (length < 1f) return;
            dir /= length;
            Vector2 perpendicular = new Vector2(-dir.y, dir.x);
            // The bridge model that crosses this segment runs perpendicular to
            // the water's own flow direction.
            bool bridgeIsHorizontal = Mathf.Abs(dir.y) >= Mathf.Abs(dir.x);

            int steps = Mathf.CeilToInt(length);
            List<Vector2Int> centerline = new List<Vector2Int>(steps + 1);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 basePoint = Vector2.Lerp(from, to, t);

                float noiseSample = Mathf.PerlinNoise((i * riverMeanderFrequency) + localSeed, 0f);
                float drift = (noiseSample - 0.5f) * 2f * riverMaxMeanderAmplitude;
                Vector2 offsetPoint = basePoint + perpendicular * drift;

                int cx = Mathf.RoundToInt(offsetPoint.x);
                int cy = Mathf.RoundToInt(offsetPoint.y);
                centerline.Add(new Vector2Int(cx, cy));

                for (int w = 0; w < thickness; w++)
                {
                    Vector2Int tile = new Vector2Int(
                        Mathf.RoundToInt(cx + perpendicular.x * w),
                        Mathf.RoundToInt(cy + perpendicular.y * w));
                    if (IsInsideBounds(tile.x, tile.y)) waterPoints.Add(tile);
                }

                // Periodic guaranteed crossing along the segment's length, same
                // cadence the old fixed-interval bridges used.
                if (i > 0 && i % Mathf.Max(1, forcedBridgeInterval) == 0)
                {
                    AddForcedBridgeAt(cx, cy, perpendicular, thickness, bridgeIsHorizontal);
                }
            }

            // Always guarantee at least one crossing per segment, even short ones.
            Vector2Int mid = centerline[centerline.Count / 2];
            AddForcedBridgeAt(mid.x, mid.y, perpendicular, thickness, bridgeIsHorizontal);
        }

        private void AddForcedBridgeAt(int cx, int cy, Vector2 perpendicular, int thickness, bool isHorizontal)
        {
            for (int w = 0; w < thickness; w++)
            {
                Vector2Int tile = new Vector2Int(
                    Mathf.RoundToInt(cx + perpendicular.x * w),
                    Mathf.RoundToInt(cy + perpendicular.y * w));
                if (!IsInsideBounds(tile.x, tile.y)) continue;

                forcedBridgeCoordinates.Add(tile);
                forcedBridgeOrientation[tile] = isHorizontal;
            }
        }

        // Hard connectivity guarantee: flood-fill every walkable tile into
        // connected components, then bridge every smaller component into the
        // largest one. The randomized water network above usually leaves the
        // map fully connected on its own, but a badly-placed pond or a tight
        // meander can still wall off a pocket — this makes it impossible for
        // that to actually happen, satisfying "traverse the whole map" as a
        // guarantee rather than a probability.
        private void EnsureFullMapConnectivity()
        {
            bool[,] visited = new bool[width, height];
            List<List<Vector2Int>> components = new List<List<Vector2Int>>();
            Vector2Int[] fourDirections =
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (visited[x, y] || !grid[x, y].IsWalkable) continue;

                    List<Vector2Int> component = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        component.Add(current);

                        foreach (Vector2Int dir in fourDirections)
                        {
                            Vector2Int next = current + dir;
                            if (!IsInsideBounds(next.x, next.y)) continue;
                            if (visited[next.x, next.y]) continue;
                            if (!grid[next.x, next.y].IsWalkable) continue;

                            visited[next.x, next.y] = true;
                            queue.Enqueue(next);
                        }
                    }

                    components.Add(component);
                }
            }

            if (components.Count <= 1) return;

            components.Sort((a, b) => b.Count - a.Count);
            List<Vector2Int> mainComponent = components[0];

            for (int i = 1; i < components.Count; i++)
            {
                ConnectComponentToMain(mainComponent, components[i]);
                mainComponent.AddRange(components[i]);
            }
        }

        private void ConnectComponentToMain(List<Vector2Int> mainComponent, List<Vector2Int> isolatedComponent)
        {
            Vector2Int bestMain = mainComponent[0];
            Vector2Int bestIsolated = isolatedComponent[0];
            float bestDistSqr = float.MaxValue;

            foreach (Vector2Int a in SampleUpTo(mainComponent, 60))
            {
                foreach (Vector2Int b in SampleUpTo(isolatedComponent, 60))
                {
                    float distSqr = (a - b).sqrMagnitude;
                    if (distSqr < bestDistSqr)
                    {
                        bestDistSqr = distSqr;
                        bestMain = a;
                        bestIsolated = b;
                    }
                }
            }

            CarveConnectivityBridge(bestMain, bestIsolated);
        }

        private IEnumerable<Vector2Int> SampleUpTo(List<Vector2Int> source, int maxCount)
        {
            if (source.Count <= maxCount)
            {
                foreach (Vector2Int item in source) yield return item;
                yield break;
            }

            int step = Mathf.Max(1, source.Count / maxCount);
            for (int i = 0; i < source.Count; i += step)
            {
                yield return source[i];
            }
        }

        // Bresenham line between two points; any water tile it crosses becomes
        // a walkable forced bridge. Non-water tiles along the way are already
        // walkable by definition — that's precisely why a straight line
        // between two disconnected components can only cross water.
        private void CarveConnectivityBridge(Vector2Int from, Vector2Int to)
        {
            Vector2 dir = new Vector2(to.x - from.x, to.y - from.y);
            bool isHorizontal = Mathf.Abs(dir.y) >= Mathf.Abs(dir.x);

            int x0 = from.x, y0 = from.y, x1 = to.x, y1 = to.y;
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                if (IsInsideBounds(x0, y0) && grid[x0, y0].Type == BiomeType.Water)
                {
                    grid[x0, y0].IsWalkable = true;
                    grid[x0, y0].MovementWeight = 1.0f;

                    Vector2Int tile = new Vector2Int(x0, y0);
                    forcedBridgeCoordinates.Add(tile);
                    forcedBridgeOrientation[tile] = isHorizontal;
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private HashSet<Vector2Int> CalculateRandomPonds(HashSet<Vector2Int> riverPoints)
        {
            HashSet<Vector2Int> pondPoints = new HashSet<Vector2Int>();

            for (int i = 0; i < pondCount; i++)
            {
                // Select a random coordinate away from the edges and the central river
                int centerX = Random.Range(5, width - 5);
                int centerY = Random.Range(5, height - 5);
                Vector2Int center = new Vector2Int(centerX, centerY);

                if (riverPoints.Contains(center)) continue;

                int radius = Random.Range(1, maxPondRadius + 1);

                // Carve a small organic circular lake shape
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (dx * dx + dy * dy <= radius * radius + 0.5f)
                        {
                            Vector2Int pondTile = new Vector2Int(centerX + dx, centerY + dy);
                            if (IsInsideBounds(pondTile.x, pondTile.y) && !riverPoints.Contains(pondTile))
                            {
                                pondPoints.Add(pondTile);
                            }
                        }
                    }
                }
            }
            return pondPoints;
        }

        private void ConstructVisualGrid(GridSaveSchema loadedSchema = null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    PathfindingNode node = grid[x, y];
                    Vector3 basePos;
                    basePos.x = node.WorldPosition.x - width / 2;
                    basePos.y = node.WorldPosition.y - cellSize / 2;
                    basePos.z = node.WorldPosition.z - height / 2;

                    Vector2Int pos = new Vector2Int(x, y);

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

                    // Force the riverSideOpen prefab for ALL water coordinates
                    GameObject selectedTile = tilePack.ground_riverSideOpen.prefab;
                    float baseRotation = 0f;
                    float appliedRiverYOffset = tilePack.ground_riverSideOpen.yOffset;

                    if (landN || landNW)
                    {
                        baseRotation = 0f;
                    }
                    else if (landE || landNE)
                    {
                        baseRotation = 90f;
                    }
                    else if (landS || landSE)
                    {
                        baseRotation = 180f;
                    }
                    else if (landW || landSW)
                    {
                        baseRotation = 270f;
                    }

                    if (selectedTile != null)
                    {
                        Vector3 riverSpawnPos = new Vector3(basePos.x, basePos.y + appliedRiverYOffset, basePos.z);
                        float finalRotation = (baseRotation + (riverRotationOffsetSteps * 90f)) % 360f;
                        SpawnVisual(selectedTile, riverSpawnPos, Quaternion.Euler(0, finalRotation, 0));
                    }

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
                        // Spawn physical bridge model on forced pathing nodes
                        if (forcedBridgeCoordinates.Contains(pos))
                        {
                            bool isHorizontal = !forcedBridgeOrientation.TryGetValue(pos, out bool storedOrientation) || storedOrientation;
                            TryPlaceSmartBridge(x, y, basePos, isHorizontal);
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
            // The list now includes 'food' with density 0.01f and yOffset 1.0f 
            // (Ensure you set these in the Inspector for your tilePack asset!)
            List<(SpawnConfiguration config, bool useRandRot)> spawnList = new List<(SpawnConfiguration, bool)>()
    {
        (tilePack.food, false), // NEW: Food added first
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
                // Defaulting density to 0.01f if not specifically configured
                float density = (item.config.density <= 0) ? 0.01f : item.config.density;

                if (item.config.prefab != null && Random.value < density)
                {
                    // Defaulting yOffset to 1.0f if not specifically configured
                    float offset = (item.config.yOffset == 0) ? 1.0f : item.config.yOffset;

                    Vector3 spawnPosition = new Vector3(basePos.x, basePos.y + offset, basePos.z);
                    float yRot = item.useRandRot ? Random.Range(0f, 360f) : 0f;

                    SpawnVisual(item.config.prefab, spawnPosition, Quaternion.Euler(0, yRot, 0));

                    // Name the object so AgentGOAP can identify it for destruction
                    GameObject lastSpawned = spawnedVisualObjects[spawnedVisualObjects.Count - 1];
                    lastSpawned.name = item.config.prefab.name;

                    // Tag the underlying grid tile so the native WorldGrid (SLAM +
                    // GOAP resource layers) actually knows a resource is here —
                    // previously these props were purely decorative.
                    grid[x, y].Type = ResolveResourceBiome(item.config.prefab);

                    return;
                }
            }
        }

        private void TryPlaceSmartBridge(int x, int y, Vector3 pos, bool isHorizontal)
        {
            if (tilePack.bridge_wood.prefab == null) return;

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

        // Maps a spawned prop back to the resource biome it represents, so the
        // native WorldGrid's wood/food/stone layers reflect what's actually on
        // the ground instead of staying empty.
        private BiomeType ResolveResourceBiome(GameObject prefab)
        {
            if (prefab == tilePack.food.prefab) return BiomeType.Food;

            if (prefab == tilePack.tree_large.prefab || prefab == tilePack.tree_small.prefab || prefab == tilePack.tree_tall.prefab)
                return BiomeType.Wood;

            if (prefab == tilePack.rock_tall_a.prefab || prefab == tilePack.rock_tall_b.prefab ||
                prefab == tilePack.stone_large_a.prefab || prefab == tilePack.stone_large_b.prefab)
                return BiomeType.Stone;

            return BiomeType.Grass;
        }

        // Tiles adjacent to a Stone deposit are rough/rubble ground: walkable
        // for everyone except WheelchairBound agents (native CellType 2, see
        // Agent::FindPath). Marked here as a decorative small-rock ring so the
        // constraint is visually obvious, not just a hidden pathing rule.
        private HashSet<Vector2Int> rubbleCoordinates = new HashSet<Vector2Int>();
        public IReadOnlyCollection<Vector2Int> RubbleTiles => rubbleCoordinates;

        private void MarkRubbleAroundStoneDeposits()
        {
            rubbleCoordinates.Clear();
            List<Vector2Int> stoneTiles = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (grid[x, y].Type == BiomeType.Stone)
                        stoneTiles.Add(new Vector2Int(x, y));

            foreach (Vector2Int stonePos in stoneTiles)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Vector2Int pos = new Vector2Int(stonePos.x + dx, stonePos.y + dy);
                        if (!IsInsideBounds(pos.x, pos.y)) continue;
                        if (grid[pos.x, pos.y].Type != BiomeType.Grass) continue;
                        if (rubbleCoordinates.Contains(pos)) continue;
                        if (Random.value >= 0.5f) continue;

                        rubbleCoordinates.Add(pos);

                        SpawnConfiguration rubbleConfig = (Random.value < 0.5f) ? tilePack.rock_small_a : tilePack.rock_small_b;
                        if (rubbleConfig.prefab != null)
                        {
                            Vector3 basePos = grid[pos.x, pos.y].WorldPosition;
                            float offset = (rubbleConfig.yOffset == 0) ? 1.0f : rubbleConfig.yOffset;
                            Vector3 spawnPos = new Vector3(basePos.x, basePos.y + offset, basePos.z);
                            SpawnVisual(rubbleConfig.prefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                        }
                    }
                }
            }
        }

        // --- QUERIES + MUTATION FOR PrimsPathNetworkBuilder ---

        public BiomeType GetBiomeAt(int x, int y)
        {
            return IsInsideBounds(x, y) ? grid[x, y].Type : BiomeType.Water;
        }

        public List<Vector2Int> FindResourceTiles(BiomeType type)
        {
            List<Vector2Int> tiles = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (grid[x, y].Type == type)
                        tiles.Add(new Vector2Int(x, y));
            return tiles;
        }

        // Lowers a tile's movement cost and, on plain grass, lays down a
        // visible path prop — used by PrimsPathNetworkBuilder to rasterize the
        // MST edges Prim's algorithm produces into an actually-cheaper route.
        public void ApplyPathTile(int x, int y)
        {
            if (!IsInsideBounds(x, y)) return;
            if (grid[x, y].Type == BiomeType.Water) return; // rivers are crossed via bridges, not paving

            grid[x, y].MovementWeight = 0.4f;

            if (grid[x, y].Type == BiomeType.Grass && tilePack.path.prefab != null)
            {
                Vector3 basePos = grid[x, y].WorldPosition;
                float offset = (tilePack.path.yOffset == 0) ? 0f : tilePack.path.yOffset;
                SpawnVisual(tilePack.path.prefab, new Vector3(basePos.x, basePos.y + offset, basePos.z), Quaternion.identity);
            }
        }

        // --- EXPORT CONTIGUOUS 1D FLAT ARRAY FOR C++ ---
        public SimpleNodeData[] ExportContiguousFlatArray()
        {
            int totalElements = width * height;
            SimpleNodeData[] flatArray = new SimpleNodeData[totalElements];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    PathfindingNode sourceNode = grid[x, y];
                    int index = (y * width) + x; // Row-major linear calculation

                    flatArray[index] = new SimpleNodeData
                    {
                        x = x,
                        y = y,
                        isWalkable = sourceNode.IsWalkable,
                        movementCost = sourceNode.MovementWeight,
                        biomeType = (int)sourceNode.Type
                    };
                }
            }

            return flatArray;
        }

        // --- GENERATES A GLOBAL PHYSICAL FLOOR ---
        private void CreateGlobalFloorCollider()
        {
            BoxCollider floor = GetComponent<BoxCollider>();
            if (floor == null)
            {
                floor = gameObject.AddComponent<BoxCollider>();
            }

            float physicalWidth = width * cellSize;
            float physicalLength = height * cellSize;

            // Make the collider cover the exact dimensions of the grid, 1 unit thick
            floor.size = new Vector3(physicalWidth, 1f, physicalLength);

            // Center the collider perfectly under the spawned tiles
            // (Tiles spawn from 0,0 upwards. Y is -0.5f so the top of the collider sits exactly at Y=0)
            floor.center = new Vector3((physicalWidth / 2f) - (cellSize / 2f), -0.5f, (physicalLength / 2f) - (cellSize / 2f));

            Debug.Log($"[TerrainGenerator] Created global BoxCollider ({physicalWidth}x{physicalLength}) for terrain physics!");
        }

        private void OnEnable()
        {
            SimulationEvents.OnNewRandomMap += GenerateNewMap;
        }

        private void OnDisable()
        {
            SimulationEvents.OnNewRandomMap -= GenerateNewMap;
        }
    }
}