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

    // Automatically configure physical perception profiles based on the assigned Role!
    _perception.visionRange = -1; // Default: Fully sighted (-1)
    _perception.canHear = true;   // Default: Can hear (true)

    if (role == AgentRole::Blind)
    {
        _perception.visionRange = 1; // Blind agent only sees immediate adjacent tiles
    }
    else if (role == AgentRole::Deaf)
    {
        _perception.canHear = false; // Deaf agent ignores sound cues entirely
    }

    // Allocate memory map for SLAM tracking
    _known.assign(static_cast<size_t>(world->GetWidth()) * world->GetHeight(), CellKnowledge::Unknown);

    if (_perception.visionRange < 0)
    {
        // Fully sighted agents (Deaf, Wheelchair, Cognitive) know the whole grid layout at start
        for (int y = 0; y < world->GetHeight(); y++)
        {
            for (int x = 0; x < world->GetWidth(); x++)
            {
                int idx = Index(x, y);
                if (world->IsBlocked(x, y)) {
                    _known[idx] = CellKnowledge::Blocked;
                }
                else if (world->HasWood(x, y)) {
                    _known[idx] = CellKnowledge::HasWood;
                }
                else if (world->HasFood(x, y)) {
                    _known[idx] = CellKnowledge::HasFood;
                }
                else {
                    _known[idx] = CellKnowledge::Free;
                }
            }
        }
    }
}

void Agent::UpdateVision(int x, int y)
{
    if (!_world) return;
    if (_perception.visionRange < 0) return; // sighted agents already know everything

    int range = _perception.visionRange;
    for (int dy = -range; dy <= range; dy++)
    {
        for (int dx = -range; dx <= range; dx++)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!_world->IsInBounds(nx, ny)) continue;

            // Circular radius so "vision range 1" reads as "the tile next
            // to me", not a slightly-too-generous 3x3 square.
            float dist = std::sqrt(static_cast<float>(dx * dx + dy * dy));
            if (dist > static_cast<float>(range) + 0.001f) continue;

            if (_world->IsBlocked(nx, ny)) {
                _known[Index(nx, ny)] = CellKnowledge::Blocked;
            }
            else if (_world->HasWood(nx, ny)) { 
                _known[Index(nx, ny)] = CellKnowledge::HasWood;
            }
            else if (_world->HasFood(nx, ny)) { 
                _known[Index(nx, ny)] = CellKnowledge::HasFood;
            }
            else {
                _known[Index(nx, ny)] = CellKnowledge::Free;
            }
        }
    }
}

float Agent::SoundPenaltyAt(int x, int y, const std::vector<SoundCue>& activeSounds) const
{
    if (!_perception.canHear) return 0.0f; // deaf: sound never influences pathing

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

    // Combined Obstacle Evaluator:
    // Respects both memory-driven Fog of War AND specialized physical role restrictions.
    auto isBlocked = [&](int x, int y) {
        // 1. Fog of War Memory Check: If the agent knows the tile is blocked, it's blocked
        if (_known[Index(x, y)] == CellKnowledge::Blocked) return true;

        // 2. Physical Constraint Check: Wheelchair users cannot traverse Stairs (CellType 2)
        if (_role == AgentRole::WheelchairBound && _world->GetCellType(x, y) == 2) return true;

        return false;
        };

    // Fail early if the start or target points are invalid/blocked
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
            if (isBlocked(nx, ny)) continue; // Skip blocked memories & stair mismatches

            int nIndex = Index(nx, ny);
            if (closed[nIndex]) continue;

            float baseMoveCost = (dx[i] != 0 && dy[i] != 0) ? diagonalCost : straightCost;
            float moveCost = baseMoveCost + SoundPenaltyAt(nx, ny, activeSounds);
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
        return result; // No path found with current knowledge

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

void Agent::MergeMemory(const Agent& otherAgent)
{
    // Ensure they are on the same world size
    if (_known.size() != otherAgent._known.size()) return;

    for (size_t i = 0; i < _known.size(); i++)
    {
        // If I don't know what this tile is, but the other agent does, learn it!
        if (_known[i] == CellKnowledge::Unknown && otherAgent._known[i] != CellKnowledge::Unknown)
        {
            _known[i] = otherAgent._known[i];
        }
    }
}