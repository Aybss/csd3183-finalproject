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

// Describes how a given agent perceives the world.
struct AgentPerception
{
    // Tiles the agent can see around itself when UpdateVision() is called.
    // -1 = normal sight (agent knows the whole grid).
    //  1 = "blind" - can only perceive the tile immediately next to it.
    // (Any non-negative value works, e.g. 3 for "short-sighted".)
    int visionRange = -1;

    // Whether the agent reacts to SoundCues at all.
    // false = "deaf" - audio cues never affect this agent's pathing,
    // even while other agents are avoiding the same cue.
    bool canHear = true;
};

enum class CellKnowledge : unsigned char
{
    Unknown = 0,
    Free = 1,
    Blocked = 2,
    HasWood = 3,   
    HasFood = 4    
};

// Wraps a shared AStarGrid (the "ground truth" world) with one agent's
// own partial knowledge of it plus its sensory traits, and plans paths
// using only what that agent actually knows/perceives -- not the true
// grid. Multiple Agents can share the same AStarGrid.
class Agent
{
public:
    void Init(AStarGrid* world, const AgentPerception& perception);

    // Reveals the true state of cells around (x, y), out to
    // perception.visionRange, into this agent's own knowledge.
    // Sighted agents (visionRange == -1) learn the entire grid the first
    // time Init() runs and never need this again unless the world
    // changes. Blind agents (small visionRange) should call this every
    // time they move, since it's the only way they learn anything.
    void UpdateVision(int x, int y);

    // Plans a path using only what this agent currently knows:
    //  - cells it has seen and knows are blocked are avoided
    //  - cells it has seen and knows are free are used normally
    //  - cells it has never perceived are assumed walkable (optimistic),
    //    so a blind agent may plan straight through a wall it hasn't
    //    "bumped into" yet. Re-call UpdateVision() + FindPath() as it
    //    moves and discovers new obstacles to get it to replan.
    // activeSounds is ignored completely if perception.canHear is false.
    std::vector<PathNode> FindPath(int startX, int startY, int endX, int endY,
                                    const std::vector<SoundCue>& activeSounds);

    const AgentPerception& GetPerception() const { return _perception; }

    void MergeMemory(const Agent& otherAgent);

private:
    AStarGrid* _world = nullptr;
    AgentPerception _perception;
    std::vector<CellKnowledge> _known;

    int Index(int x, int y) const;
    float SoundPenaltyAt(int x, int y, const std::vector<SoundCue>& activeSounds) const;
};
