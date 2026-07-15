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
// Passed down from the agent definition. The planner and action cost
// functions read this to produce disability-aware behaviour.
// ---------------------------------------------------------------------------
enum class DisabilityType {
    None,
    Blind,          // The Forager
    Deaf,           // The Lumber Collector
    WheelchairUser, // The Builder
    CognitiveDifficulty
};

struct AgentProfile {
    DisabilityType disability = DisabilityType::None;

    // Survival Senses (Used by Perception sweeps and Fog of War clearing)
    int sightRadius = 5;
    int hearingRange = 5;

    // Dynamic stats
    float fatigue = 0.0f;
    float cognitiveLoad = 0.0f;

    // Movement modifiers (Added movementCostMultiplier!)
    float movementCostMultiplier = 1.0f;   // Slower agents (like the Builder) have a higher multiplier
    float stairCostMultiplier = 1.0f;   // WheelchairUser -> set to 1e9 (impassable)
    float rampCostMultiplier = 1.0f;
    float wallAdjacentBonus = 1.0f;
    float tactilePavingBonus = 1.0f;
    float crowdCostMultiplier = 1.0f;

    // --- Factory helpers -------------------------------------------------
    static AgentProfile MakeBlind() {
        AgentProfile p;
        p.disability = DisabilityType::Blind;
        p.sightRadius = 1;
        p.hearingRange = 8;
        p.movementCostMultiplier = 1.0f; // Standard speed
        p.wallAdjacentBonus = 1.0f;
        p.tactilePavingBonus = 1.0f;
        return p;
    }

    static AgentProfile MakeWheelchairUser() {
        AgentProfile p;
        p.disability = DisabilityType::WheelchairUser;
        p.sightRadius = 4;
        p.hearingRange = 4;
        p.movementCostMultiplier = 2.5f; // Builder is significantly slower due to heavy transport
        p.stairCostMultiplier = 1e9f;
        p.rampCostMultiplier = 1.0f;
        return p;
    }

    static AgentProfile MakeDeaf() {
        AgentProfile p;
        p.disability = DisabilityType::Deaf;
        p.sightRadius = 7;
        p.hearingRange = 0;
        p.movementCostMultiplier = 0.8f; // Lumberjack moves slightly faster to haul wood
        return p;
    }

    static AgentProfile MakeCognitiveDifficulty() {
        AgentProfile p;
        p.disability = DisabilityType::CognitiveDifficulty;
        p.cognitiveLoad = 0.3f;
        p.crowdCostMultiplier = 2.0f;
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

    // Set a fact
    void Set(const std::string& key, bool value) {
        facts[key] = value;
    }

    // Read a fact (defaults to false if unknown)
    bool Get(const std::string& key) const {
        auto it = facts.find(key);
        return (it != facts.end()) ? it->second : false;
    }

    // Returns true if every fact in 'other' matches this state.
    // Used by the planner to check preconditions and detect goal satisfaction.
    bool Satisfies(const WorldState& other) const {
        for (const auto& [key, value] : other.facts) {
            if (Get(key) != value) return false;
        }
        return true;
    }

    // Apply the effects of an action: copy this state and overlay new facts.
    WorldState ApplyEffects(const WorldState& effects) const {
        WorldState next = *this;  // copy
        for (const auto& [key, value] : effects.facts) {
            next.facts[key] = value;
        }
        return next;
    }
};

// ---------------------------------------------------------------------------
// Blackboard
// Holds the agent's real numeric runtime state — hunger, fatigue, inventory.
// ---------------------------------------------------------------------------
struct Blackboard {
    // Survival metrics
    float hungerLevel = 100.0f;  // 100 = full, 0 = starving
    float fatigueLevel = 0.0f;    // 0 = rested, 100 = exhausted

    // Inventory
    int carriedWood = 0;
    int carriedFood = 0;
    int maxWoodCapacity = 5;
    int maxFoodCapacity = 5;

    // Drain rates — set from AgentProfile (wheelchair user drains fatigue 2x)
    float hungerDrainRate = 1.0f;
    float fatigueDrainRate = 1.0f;

    // Convenience checks
    bool IsStarving()            const { return hungerLevel <= 0.0f; }
    bool IsExhausted()         const { return fatigueLevel >= 100.0f; }
    bool IsInventoryFullOfWood() const { return carriedWood >= maxWoodCapacity; }

    // Called every simulation tick to drain hunger and apply fatigue.
    void UpdateTick(float deltaTime) {
        hungerLevel -= hungerDrainRate * deltaTime;
        if (hungerLevel < 0.0f) hungerLevel = 0.0f;

        fatigueLevel += fatigueDrainRate * deltaTime;
        if (fatigueLevel > 100.0f) fatigueLevel = 100.0f;
    }

    // Convert numeric state into boolean WorldState facts for the GOAP planner.
    void PopulateFacts(WorldState& state) const {
        state.Set("hunger_critical", hungerLevel <= 20.0f);
        state.Set("fatigue_high", fatigueLevel >= 80.0f);
        state.Set("has_wood", carriedWood > 0);
        state.Set("has_food", carriedFood > 0);
        state.Set("inventory_full", IsInventoryFullOfWood());
    }
};