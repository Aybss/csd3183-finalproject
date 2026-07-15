#pragma once
#include <vector>

struct PathNode
{
    int x;
    int y;
};

// A basic 8-directional grid-based A* pathfinder.
// Operates on a simple occupancy grid (blocked/free cells), matching
// the data exported by Unity's ObstacleGrid.ExportOccupancyBytes().
class AStarGrid
{
public:
    void Init(int width, int height);

    // Marks a single cell as blocked/free.
    void SetBlocked(int x, int y, bool blocked);

    // Sets/Gets a custom environmental cell type (e.g., 0 = Free, 1 = Blocked, 2 = Stairs)
    void SetCellType(int x, int y, int cellType);
    int GetCellType(int x, int y) const;

    // Bulk-loads an occupancy grid from a flat byte array
    // (0 = free, non-zero = blocked).
    void LoadFromBytes(const unsigned char* data, int length);

    bool IsBlocked(int x, int y) const;
    bool IsInBounds(int x, int y) const;

    int GetWidth() const { return _width; }
    int GetHeight() const { return _height; }

    // Finds a path from start to end (inclusive). Returns an empty
    // vector if no path exists or the inputs are invalid.
    std::vector<PathNode> FindPath(int startX, int startY, int endX, int endY);

private:
    int _width = 0;
    int _height = 0;
    std::vector<bool> _blocked;
    std::vector<int> _cellTypes; // Parallel vector tracking unique terrain types

    int Index(int x, int y) const { return x + y * _width; }
};