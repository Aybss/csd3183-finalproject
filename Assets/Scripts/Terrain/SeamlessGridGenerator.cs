using System.Collections.Generic;
using UnityEngine;

public class SeamlessGridGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct TileVisuals
    {
        public GameObject grassPrefab;
        public GameObject waterCenter;
        public GameObject waterEdgeStraight;
        public GameObject waterCornerOuter;
        public GameObject waterCornerInner;
        public GameObject woodResource;
        public GameObject foodResource;
    }

    [Header("Grid Configurations")]
    public int width = 30;
    public int height = 30;
    public float cellSize = 1f;

    [Header("Perlin Configuration")]
    public float noiseScale = 0.12f;
    public float waterThreshold = 0.35f;
    public float woodThreshold = 0.65f;
    public float foodThreshold = 0.80f;

    [Header("Prefabs Assignment")]
    public TileVisuals prefabs;

    private GridNode[,] grid;

    private void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        grid = new GridNode[width, height];
        float offsetX = Random.Range(0f, 5000f);
        float offsetY = Random.Range(0f, 5000f);

        // PASS 1: Initialize Data Profile Values 
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = new Vector3(x * cellSize, 0, y * cellSize);
                float sampleX = (x * noiseScale) + offsetX;
                float sampleY = (y * noiseScale) + offsetY;
                float noise = Mathf.PerlinNoise(sampleX, sampleY);

                TileType type = TileType.Grass;
                float weight = 1.0f;

                if (noise < waterThreshold)
                {
                    type = TileType.Water;
                    weight = 5.0f;
                }
                else if (noise > foodThreshold)
                {
                    type = TileType.Food;
                    weight = 1.2f;
                }
                else if (noise > woodThreshold)
                {
                    type = TileType.Wood;
                    weight = 1.8f;
                }

                grid[x, y] = new GridNode(new Vector2Int(x, y), worldPos, type, weight, true);
            }
        }

        // PASS 2: Match Visual Connectivity Based on Adjacencies
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ConstructTileVisuals(x, y);
            }
        }
    }

    private void ConstructTileVisuals(int x, int y)
    {
        GridNode currentNode = grid[x, y];
        Vector3 spawnPos = currentNode.WorldPosition;

        // Base Underlayment for Resources
        if (currentNode.Type != TileType.Water)
        {
            Instantiate(prefabs.grassPrefab, spawnPos, Quaternion.identity, transform);

            if (currentNode.Type == TileType.Wood)
                Instantiate(prefabs.woodResource, spawnPos, Quaternion.identity, transform);
            else if (currentNode.Type == TileType.Food)
                Instantiate(prefabs.foodResource, spawnPos, Quaternion.identity, transform);

            return;
        }

        // Calculate Cardinal Water Connections (North, East, South, West)
        bool N = IsWaterTile(x, y + 1);
        bool E = IsWaterTile(x + 1, y);
        bool S = IsWaterTile(x, y - 1);
        bool W = IsWaterTile(x - 1, y);

        int connectionCount = (N ? 1 : 0) + (E ? 1 : 0) + (S ? 1 : 0) + (W ? 1 : 0);

        GameObject selectedPrefab = prefabs.waterCenter;
        float yRotation = 0f;

        switch (connectionCount)
        {
            case 4: // Surrounded completely by water
                selectedPrefab = prefabs.waterCenter;
                break;

            case 3: // Three water neighbors = Straight Shore Edge on the missing side
                selectedPrefab = prefabs.waterEdgeStraight;
                if (!N) yRotation = 0f;
                if (!E) yRotation = 90f;
                if (!S) yRotation = 180f;
                if (!W) yRotation = 270f;
                break;

            case 2:
                // Check if it's a straight parallel channel canal or an L-Corner
                if (N && S) { selectedPrefab = prefabs.waterEdgeStraight; yRotation = 90f; }
                else if (E && W) { selectedPrefab = prefabs.waterEdgeStraight; yRotation = 0f; }
                else
                {
                    // L-shaped Corner connections (Inner turns)
                    selectedPrefab = prefabs.waterCornerInner;
                    if (N && E) yRotation = 0f;
                    if (E && S) yRotation = 90f;
                    if (S && W) yRotation = 180f;
                    if (W && N) yRotation = 270f;
                }
                break;

            case 1: // Single water connector creates an Outer Corner tip
                selectedPrefab = prefabs.waterCornerOuter;
                if (N) yRotation = 0f;
                if (E) yRotation = 90f;
                if (S) yRotation = 180f;
                if (W) yRotation = 270f;
                break;

            case 0: // Isolated single water node pond
                selectedPrefab = prefabs.waterCenter;
                break;
        }

        Instantiate(selectedPrefab, spawnPos, Quaternion.Euler(0, yRotation, 0), transform);
    }

    private bool IsWaterTile(int x, int y)
    {
        // Treat Map boundary edges as water so liquid maps wrap naturally
        if (x < 0 || x >= width || y < 0 || y >= height) return true;
        return grid[x, y].Type == TileType.Water;
    }
}