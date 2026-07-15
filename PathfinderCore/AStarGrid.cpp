#include "AStarGrid.h"
#include <queue>
#include <cmath>
#include <algorithm>

void AStarGrid::Init(int width, int height)
{
    _width = width;
    _height = height;

    size_t size = static_cast<size_t>(width) * height;
    _blocked.assign(size, false);
    _cellTypes.assign(size, 0); // Default all cells to Type 0 (Free)
}

void AStarGrid::SetBlocked(int x, int y, bool blocked)
{
    if (!IsInBounds(x, y)) return;
    int idx = Index(x, y);
    _blocked[idx] = blocked;
    _cellTypes[idx] = blocked ? 1 : 0; // Synchronize cell type
}

void AStarGrid::SetCellType(int x, int y, int cellType)
{
    if (!IsInBounds(x, y)) return;
    int idx = Index(x, y);
    _cellTypes[idx] = cellType;

    // Automatically keep standard blockages in sync:
    // CellType 1 is Blocked. CellType 0 (Free) and CellType 2 (Stairs) are unblocked.
    _blocked[idx] = (cellType == 1);
}

int AStarGrid::GetCellType(int x, int y) const
{
    if (!IsInBounds(x, y)) return 1; // Out of bounds behaves as Blocked (Type 1)
    return _cellTypes[Index(x, y)];
}

void AStarGrid::LoadFromBytes(const unsigned char* data, int length)
{
    int count = std::min(length, static_cast<int>(_blocked.size()));
    for (int i = 0; i < count; i++)
    {
        _blocked[i] = (data[i] != 0);
        _cellTypes[i] = (data[i] != 0) ? 1 : 0;
    }
}

bool AStarGrid::IsBlocked(int x, int y) const
{
    if (!IsInBounds(x, y)) return true; // treat out-of-bounds as blocked
    return _blocked[Index(x, y)];
}

bool AStarGrid::IsInBounds(int x, int y) const
{
    return x >= 0 && x < _width && y >= 0 && y < _height;
}

namespace
{
    struct OpenEntry
    {
        int index;
        float fCost;
    };

    struct CompareOpenEntry
    {
        // std::priority_queue is a max-heap by default; flip the
        // comparison so the LOWEST fCost comes out first.
        bool operator()(const OpenEntry& a, const OpenEntry& b) const
        {
            return a.fCost > b.fCost;
        }
    };

    float Heuristic(int x1, int y1, int x2, int y2)
    {
        float dx = static_cast<float>(x2 - x1);
        float dy = static_cast<float>(y2 - y1);
        return std::sqrt(dx * dx + dy * dy);
    }
}

std::vector<PathNode> AStarGrid::FindPath(int startX, int startY, int endX, int endY)
{
    std::vector<PathNode> result;

    if (!IsInBounds(startX, startY) || !IsInBounds(endX, endY)) return result;
    if (IsBlocked(startX, startY) || IsBlocked(endX, endY)) return result;

    int startIndex = Index(startX, startY);
    int endIndex = Index(endX, endY);
    int cellCount = _width * _height;

    std::vector<float> gCost(cellCount, -1.0f);
    std::vector<int> cameFrom(cellCount, -1);
    std::vector<bool> closed(cellCount, false);

    std::priority_queue<OpenEntry, std::vector<OpenEntry>, CompareOpenEntry> openSet;

    gCost[startIndex] = 0.0f;
    openSet.push({ startIndex, Heuristic(startX, startY, endX, endY) });

    // 8-directional movement: 4 straight + 4 diagonal neighbors.
    const int dx[8] = { 1, -1, 0, 0, 1, 1, -1, -1 };
    const int dy[8] = { 0, 0, 1, -1, 1, -1, 1, -1 };
    const float straightCost = 1.0f;
    const float diagonalCost = 1.41421356f;

    while (!openSet.empty())
    {
        OpenEntry current = openSet.top();
        openSet.pop();

        if (closed[current.index]) continue;
        closed[current.index] = true;

        if (current.index == endIndex) break;

        int cx = current.index % _width;
        int cy = current.index / _width;

        for (int i = 0; i < 8; i++)
        {
            int nx = cx + dx[i];
            int ny = cy + dy[i];

            if (!IsInBounds(nx, ny) || IsBlocked(nx, ny)) continue;

            // Prevent cutting across a diagonal gap between two blocked orthogonal
            // cells (walking "through" a wall corner). Both flanking cells must be open.
            bool isDiagonal = dx[i] != 0 && dy[i] != 0;
            if (isDiagonal)
            {
                bool horizontalOpen = IsInBounds(cx + dx[i], cy) && !IsBlocked(cx + dx[i], cy);
                bool verticalOpen = IsInBounds(cx, cy + dy[i]) && !IsBlocked(cx, cy + dy[i]);
                if (!horizontalOpen || !verticalOpen) continue;
            }

            int nIndex = Index(nx, ny);
            if (closed[nIndex]) continue;

            float moveCost = (dx[i] != 0 && dy[i] != 0) ? diagonalCost : straightCost;
            float tentativeG = gCost[current.index] + moveCost;

            if (gCost[nIndex] < 0.0f || tentativeG < gCost[nIndex])
            {
                gCost[nIndex] = tentativeG;
                cameFrom[nIndex] = current.index;
                float f = tentativeG + Heuristic(nx, ny, endX, endY);
                openSet.push({ nIndex, f });
            }
        }
    }

    if (startIndex != endIndex && cameFrom[endIndex] == -1)
        return result; // no path found

    // Walk backwards from end to start, then reverse.
    std::vector<int> indexPath;
    int walker = endIndex;
    indexPath.push_back(walker);
    while (walker != startIndex)
    {
        walker = cameFrom[walker];
        if (walker == -1) return {}; // safety guard, shouldn't happen
        indexPath.push_back(walker);
    }
    std::reverse(indexPath.begin(), indexPath.end());

    for (int idx : indexPath)
        result.push_back({ idx % _width, idx / _width });

    return result;
}