#include "Agent.h"
#include <queue>
#include <cmath>
#include <algorithm>

int Agent::Index(int x, int y) const
{
    return x + y * _world->GetWidth();
}

void Agent::Init(AStarGrid* world, AgentRole role)
{
    _world = world;
    _role = role;
}

float Agent::SoundPenaltyAt(int x, int y, const std::vector<SoundCue>& activeSounds) const
{
    if (_role == AgentRole::Deaf) return 0.0f; // Deaf: completely ignore sound cues

    float penalty = 0.0f;
    float fx = static_cast<float>(x);
    float fy = static_cast<float>(y);
    for (const auto& cue : activeSounds)
    {
        float dx = fx - cue.x;
        float dy = fy - cue.y;
        float dist = std::sqrt(dx * dx + dy * dy);
        if (dist <= cue.radius)
            penalty += cue.costPenalty;
    }
    return penalty;
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

std::vector<PathNode> Agent::FindPath(int startX, int startY, int endX, int endY,
    const std::vector<SoundCue>& activeSounds)
{
    std::vector<PathNode> result;
    if (!_world) return result;
    if (!_world->IsInBounds(startX, startY) || !_world->IsInBounds(endX, endY)) return result;

    int width = _world->GetWidth();
    int height = _world->GetHeight();

    // Lambda to check standard blockages and custom role obstacles
    auto isBlocked = [&](int x, int y) {
        if (_world->IsBlocked(x, y)) return true;

        // WheelchairBound cannot traverse Stairs (represented by CellType 2)
        if (_role == AgentRole::WheelchairBound && _world->GetCellType(x, y) == 2) return true;

        return false;
        };

    if (isBlocked(startX, startY) || isBlocked(endX, endY)) return result;

    int startIndex = Index(startX, startY);
    int endIndex = Index(endX, endY);
    int cellCount = width * height;

    std::vector<float> gCost(cellCount, -1.0f);
    std::vector<int> cameFrom(cellCount, -1);
    std::vector<bool> closed(cellCount, false);

    std::priority_queue<OpenEntry, std::vector<OpenEntry>, CompareOpenEntry> openSet;

    gCost[startIndex] = 0.0f;
    openSet.push({ startIndex, Heuristic(startX, startY, endX, endY) });

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

        int cx = current.index % width;
        int cy = current.index / width;

        for (int i = 0; i < 8; i++)
        {
            int nx = cx + dx[i];
            int ny = cy + dy[i];

            if (!_world->IsInBounds(nx, ny)) continue;
            if (isBlocked(nx, ny)) continue;

            int nIndex = Index(nx, ny);
            if (closed[nIndex]) continue;

            float baseMoveCost = (dx[i] != 0 && dy[i] != 0) ? diagonalCost : straightCost;
            float weightedMoveCost = baseMoveCost * _world->GetCellWeight(nx, ny);
            float moveCost = weightedMoveCost + SoundPenaltyAt(nx, ny, activeSounds);
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
        return result;

    std::vector<int> indexPath;
    int walker = endIndex;
    indexPath.push_back(walker);
    while (walker != startIndex)
    {
        walker = cameFrom[walker];
        if (walker == -1) return {};
        indexPath.push_back(walker);
    }
    std::reverse(indexPath.begin(), indexPath.end());

    for (int idx : indexPath)
        result.push_back({ idx % width, idx / width });

    return result;
}