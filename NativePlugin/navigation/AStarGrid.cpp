#include "AStarGrid.h"
#include <queue>
#include <cmath>
#include <algorithm>

void AStarGrid::Init(int width, int height)
{
    _width = width;
    _height = height;
    _blocked.assign(static_cast<size_t>(width) * height, false);

    // NEW: Initialize resource buffers with 0 resources[cite: 3]
    _wood.assign(static_cast<size_t>(width) * height, 0);
    _food.assign(static_cast<size_t>(width) * height, 0);
}

void AStarGrid::SetBlocked(int x, int y, bool blocked)
{
    if (!IsInBounds(x, y)) return;
    _blocked[Index(x, y)] = blocked;
}

void AStarGrid::SetWood(int x, int y, int amount)
{
    if (!IsInBounds(x, y)) return;
    _wood[Index(x, y)] = amount;
}

void AStarGrid::SetFood(int x, int y, int amount)
{
    if (!IsInBounds(x, y)) return;
    _food[Index(x, y)] = amount;
}

void AStarGrid::LoadFromBytes(const unsigned char* data, int length)
{
    int count = std::min(length, static_cast<int>(_blocked.size()));
    for (int i = 0; i < count; i++)
        _blocked[i] = (data[i] != 0);
}

bool AStarGrid::IsBlocked(int x, int y) const
{
    if (!IsInBounds(x, y)) return true; // treat out-of-bounds as blocked
    return _blocked[Index(x, y)];
}

bool AStarGrid::HasWood(int x, int y) const
{
    if (!IsInBounds(x, y)) return false;
    return _wood[Index(x, y)] > 0;
}

bool AStarGrid::HasFood(int x, int y) const
{
    if (!IsInBounds(x, y)) return false;
    return _food[Index(x, y)] > 0;
}

int AStarGrid::GetWoodAmount(int x, int y) const
{
    if (!IsInBounds(x, y)) return 0;
    return _wood[Index(x, y)];
}

int AStarGrid::GetFoodAmount(int x, int y) const
{
    if (!IsInBounds(x, y)) return 0;
    return _food[Index(x, y)];
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
