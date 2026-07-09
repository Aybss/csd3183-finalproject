#include "AStarGrid.h"

// A single grid instance shared by all exported functions. Simple and
// fine for now — if you later need multiple independent grids (e.g.
// per-level), swap this for a handle-based system.
static AStarGrid g_grid;

// The extern "C" block prevents C++ from name-mangling these
// functions, so C# can find them by their exact text name via
// [DllImport].
extern "C" {

    __declspec(dllexport) int InitializeGrid(int width, int height)
    {
        g_grid.Init(width, height);
        return 1; // 1 = success
    }

    // blocked: 0 = free, non-zero = blocked. (Using int instead of
    // bool here avoids C#/C++ bool marshaling pitfalls.)
    __declspec(dllexport) void SetCellBlocked(int x, int y, int blocked)
    {
        g_grid.SetBlocked(x, y, blocked != 0);
    }

    // Bulk-loads an occupancy grid. `data` should be a flat byte array
    // matching Unity's ObstacleGrid.ExportOccupancyBytes() output
    // (0 = free, 1 = blocked), and `length` must match width*height
    // from InitializeGrid.
    __declspec(dllexport) void LoadObstacleData(unsigned char* data, int length)
    {
        g_grid.LoadFromBytes(data, length);
    }

    // Finds a path and writes the result into caller-provided arrays.
    // outX/outY must each be pre-allocated by the caller with at
    // least maxPathLength ints.
    //
    // Returns:
    //   >= 0  the number of cells written to outX/outY
    //   -1    no path exists between start and end
    //   -2    a path exists but is longer than maxPathLength
    //         (increase the buffer size and try again)
    __declspec(dllexport) int FindPath(int startX, int startY, int endX, int endY,
                                        int* outX, int* outY, int maxPathLength)
    {
        std::vector<PathNode> path = g_grid.FindPath(startX, startY, endX, endY);

        if (path.empty()) return -1;
        if (static_cast<int>(path.size()) > maxPathLength) return -2;

        for (size_t i = 0; i < path.size(); i++)
        {
            outX[i] = path[i].x;
            outY[i] = path[i].y;
        }

        return static_cast<int>(path.size());
    }
}
