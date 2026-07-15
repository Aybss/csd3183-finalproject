// goap/WorldState.h
//
// The WorldState is the "Blackboard" — a flat snapshot of boolean facts
// about the world and agent. The GOAP planner reads this to decide which
// actions are available and what the goal looks like.

#pragma once
#include <unordered_map>
#include <string>

// ---------------------------------------------------------------------------
// Agent capability profile
// Derived from the AgentRole assigned at CreateAgent(). The planner and
// action cost functions read this to produce impairment-aware behaviour.
// ---------------------------------------------------------------------------
enum class DisabilityType {
    None,
    Blind,
    Deaf,
    WheelchairUser
};

struct AgentProfile {
    DisabilityType disability = DisabilityType::None;

    // Survival senses (used by Perception sweeps and Fog of War clearing)
    int sightRadius = 5;
    int hearingRange = 5;

    // Dynamic stats
    float fatigue = 0.0f;
    float cognitiveLoad = 0.0f;

    // Movement modifiers
    float movementCostMultiplier = 1.0f;   // Slower agents have a higher multiplier
    float stairCostMultiplier = 1.0f;      // WheelchairUser can't use rubble/stairs; handled via CellType, not cost

    // --- Factory helpers -------------------------------------------------
    static AgentProfile MakeBlind() {
        AgentProfile p;
        p.disability = DisabilityType::Blind;
        p.sightRadius = 1;
        p.hearingRange = 8;
        p.movementCostMultiplier = 1.0f;
        return p;
    }

    static AgentProfile MakeWheelchairUser() {
        AgentProfile p;
        p.disability = DisabilityType::WheelchairUser;
        p.sightRadius = 5;
        p.hearingRange = 5;
        p.movementCostMultiplier = 1.6f; // Slower overall due to terrain constraints
        p.stairCostMultiplier = 1e9f;
        return p;
    }

    static AgentProfile MakeDeaf() {
        AgentProfile p;
        p.disability = DisabilityType::Deaf;
        p.sightRadius = 7;
        p.hearingRange = 0;
        p.movementCostMultiplier = 0.9f;
        return p;
    }
};

// ---------------------------------------------------------------------------
// WorldState
// A flat map of named boolean facts. Cheap to copy (used heavily during
// the GOAP planner's internal search).
// ---------------------------------------------------------------------------
struct WorldState {
    std::unordered_map<std::string, bool> facts;

    void Set(const std::string& key, bool value) {
        facts[key] = value;
    }

    bool Get(const std::string& key) const {
        auto it = facts.find(key);
        return (it != facts.end()) ? it->second : false;
    }

    // Returns true if every fact in 'other' matches this state.
    // Used by the planner to check preconditions and detect goal satisfaction.
    bool Satisfies(const WorldState& other) const {
        for (const auto& pair : other.facts) {
            if (Get(pair.first) != pair.second) return false;
        }
        return true;
    }

    // Apply the effects of an action: copy this state and overlay new facts.
    WorldState ApplyEffects(const WorldState& effects) const {
        WorldState next = *this;
        for (const auto& pair : effects.facts) {
            next.facts[pair.first] = pair.second;
        }
        return next;
    }
};

// ---------------------------------------------------------------------------
// Blackboard
// Holds the agent's real numeric runtime state — hunger, thirst, fatigue,
// inventory. Synced from Unity's AgentStats each decision tick via
// SyncAgentBlackboard() so the GOAP planner reasons about up-to-date state.
// ---------------------------------------------------------------------------
struct Blackboard {
    // Survival metrics (0-100 scale, matching Unity's AgentStats)
    float hunger = 0.0f;
    float thirst = 0.0f;
    float fatigue = 0.0f;

    // Inventory
    int carriedWood = 0;
    int carriedStone = 0;
    int maxWoodCapacity = 3;
    int maxStoneCapacity = 3;

    // Convert numeric state into boolean WorldState facts for the GOAP planner.
    void PopulateFacts(WorldState& state) const {
        state.Set("thirst_critical", thirst > 75.0f);
        state.Set("hunger_critical", hunger > 80.0f);
        state.Set("fatigue_high", fatigue > 85.0f);
        state.Set("has_wood", carriedWood > 0);
        state.Set("has_stone", carriedStone > 0);
        state.Set("wood_full", carriedWood >= maxWoodCapacity);
        state.Set("stone_full", carriedStone >= maxStoneCapacity);
    }
};
