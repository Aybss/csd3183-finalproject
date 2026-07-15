using System.Collections.Generic;
using UnityEngine;

public class ProceduralGeneration : MonoBehaviour
{
    [Header("Maze Settings")]
    public int width = 21;    // Keep odd
    public int height = 21;   // Keep odd
    public float cellSize = 3f; // Kenney's kit tiles are usually 3x3 units (adjust if needed)

    [Header("Modular Cave Kit Prefabs")]
    public GameObject caveSolidWall; // Spawned where there is no path (Solid Rock)
    public GameObject caveDeadEnd;   // 1 open side
    public GameObject caveStraight;  // 2 open sides (opposite)
    public GameObject caveCorner;    // 2 open sides (adjacent)
    public GameObject caveTJunction; // 3 open sides
    public GameObject caveCross;     // 4 open sides

    [Header("Hierarchy Container")]
    public Transform mazeParent;

    private byte[,] _mazeGrid; // 0 = Path, 1 = Wall

    public void GenerateNewMaze()
    {
        if (mazeParent != null)
        {
            foreach (Transform child in mazeParent)
            {
                Destroy(child.gameObject);
            }
        }

        _mazeGrid = new byte[width, height];

        // 1. Fill grid with walls
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                _mazeGrid[x, y] = 1;
            }
        }

        // 2. Carve paths
        CarveMaze(1, 1);

        // 3. Sync Native C++ Grid
        NativeBridge.InitializeGrid(width, height);

        // 4. Place modular cave tiles dynamically
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(x * cellSize, 0f, y * cellSize);

                if (_mazeGrid[x, y] == 1)
                {
                    // Spawn filler rock walls for blocked areas
                    if (caveSolidWall != null)
                    {
                        Instantiate(caveSolidWall, position, Quaternion.identity, mazeParent);
                    }
                    NativeBridge.SetCellBlocked(x, y, 1); //
                }
                else
                {
                    // It's a path cell! Compute connection directions to place the right cave kit piece
                    SpawnModularCaveTile(x, y, position);
                    NativeBridge.SetCellBlocked(x, y, 0); //
                }
            }
        }

        Debug.Log($"[ProceduralGeneration] Seamless modular cave maze generated!");
    }

    private void SpawnModularCaveTile(int x, int y, Vector3 position)
    {
        // 4-Way Neighbor Check (1 = Path, 0 = Solid Wall)
        // Bit positions: North = 1, East = 2, South = 4, West = 8
        int mask = 0;

        if (y + 1 < height && _mazeGrid[x, y + 1] == 0) mask |= 1; // North
        if (x + 1 < width && _mazeGrid[x + 1, y] == 0) mask |= 2; // East
        if (y - 1 >= 0 && _mazeGrid[x, y - 1] == 0) mask |= 4; // South
        if (x - 1 >= 0 && _mazeGrid[x - 1, y] == 0) mask |= 8; // West

        GameObject prefabToSpawn = null;
        float yRotation = 0f;

        // Map the 16 possible neighborhood shapes to Kenney's prefabs and rotations
        switch (mask)
        {
            // --- DEAD ENDS (1 connection) ---
            case 1: prefabToSpawn = caveDeadEnd; yRotation = 0f; break; // Open North
            case 2: prefabToSpawn = caveDeadEnd; yRotation = 90f; break; // Open East
            case 4: prefabToSpawn = caveDeadEnd; yRotation = 180f; break; // Open South
            case 8: prefabToSpawn = caveDeadEnd; yRotation = 270f; break; // Open West

            // --- STRAIGHTS (2 opposite connections) ---
            case 5: prefabToSpawn = caveStraight; yRotation = 0f; break; // North-South
            case 10: prefabToSpawn = caveStraight; yRotation = 90f; break; // East-West

            // --- CORNERS (2 adjacent connections) ---
            case 3: prefabToSpawn = caveCorner; yRotation = 0f; break; // North-East
            case 6: prefabToSpawn = caveCorner; yRotation = 90f; break; // East-South
            case 12: prefabToSpawn = caveCorner; yRotation = 180f; break; // South-West
            case 9: prefabToSpawn = caveCorner; yRotation = 270f; break; // West-North

            // --- T-JUNCTIONS (3 connections) ---
            case 7: prefabToSpawn = caveTJunction; yRotation = 0f; break; // N-E-S (Left wall closed)
            case 14: prefabToSpawn = caveTJunction; yRotation = 90f; break; // E-S-W (Top wall closed)
            case 13: prefabToSpawn = caveTJunction; yRotation = 180f; break; // S-W-N (Right wall closed)
            case 11: prefabToSpawn = caveTJunction; yRotation = 270f; break; // W-N-E (Bottom wall closed)

            // --- CROSS JUNCTION (4 connections) ---
            case 15: prefabToSpawn = caveCross; yRotation = 0f; break;

            // Fallbacks (Single isolated paths default to dead ends)
            default: prefabToSpawn = caveDeadEnd; yRotation = 0f; break;
        }

        if (prefabToSpawn != null)
        {
            Instantiate(prefabToSpawn, position, Quaternion.Euler(0f, yRotation, 0f), mazeParent);
        }
    }

    private void CarveMaze(int cx, int cy)
    {
        _mazeGrid[cx, cy] = 0;

        List<Vector2Int> dirs = new List<Vector2Int>
        {
            new Vector2Int(0, 2),
            new Vector2Int(0, -2),
            new Vector2Int(2, 0),
            new Vector2Int(-2, 0)
        };

        for (int i = 0; i < dirs.Count; i++)
        {
            Vector2Int temp = dirs[i];
            int randomIndex = Random.Range(i, dirs.Count);
            dirs[i] = dirs[randomIndex];
            dirs[randomIndex] = temp;
        }

        foreach (Vector2Int dir in dirs)
        {
            int nx = cx + dir.x;
            int ny = cy + dir.y;

            if (nx > 0 && nx < width - 1 && ny > 0 && ny < height - 1)
            {
                if (_mazeGrid[nx, ny] == 1)
                {
                    _mazeGrid[cx + dir.x / 2, cy + dir.y / 2] = 0;
                    CarveMaze(nx, ny);
                }
            }
        }
    }

    private void Start()
    {
        GenerateNewMaze();
    }
}