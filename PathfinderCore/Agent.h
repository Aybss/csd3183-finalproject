#pragma once
#include <vector>
#include "AStarGrid.h"

// A world-space sound event (footsteps, an alarm, a gunshot, etc).
// Agents that can hear treat cells within `radius` of the cue as more
// costly to path through, modeling caution around a sound they noticed.
// Agents that are deaf ignore SoundCues entirely.
struct SoundCue
{
    float x = 0.0f;
    float y = 0.0f;
    float radius = 0.0f;
    float costPenalty = 0.0f;
};

// Exclusive physical agent traits/disabilities
enum class AgentRole : int
{
    WheelchairBound = 0,
    Blind = 1,
    Deaf = 2
};

// CellType codes set from Unity (see GridCoordinator.SyncProceduralGridWithNative):
// 2 = rubble (impassable for WheelchairBound), 3 = water crossing / bridge
// (costly, not blocking, for WheelchairBound). Default/unset is 0.
namespace CellTypeCode
{
    constexpr int Rubble = 2;
    constexpr int WaterCrossing = 3;
}

// Wraps a shared AStarGrid with one agent's role constraints.
class Agent
{
public:
    void Init(AStarGrid* world, AgentRole role);

    // Plans a path using the shared grid but respecting exclusive role constraints
    std::vector<PathNode> FindPath(int startX, int startY, int endX, int endY,
        const std::vector<SoundCue>& activeSounds);

    AgentRole GetRole() const { return _role; }

private:
    AStarGrid* _world = nullptr;
    AgentRole _role = AgentRole::WheelchairBound;

    int Index(int x, int y) const;
    float SoundPenaltyAt(int x, int y, const std::vector<SoundCue>& activeSounds) const;
    float RoleCellCostMultiplier(int x, int y) const;
};