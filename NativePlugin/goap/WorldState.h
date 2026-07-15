// WorldState.h
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
    Blind,
    Deaf,
    WheelchairUser,
    CognitiveDifficulty
};

struct AgentProfile {
    DisabilityType disability = DisabilityType::None;

    // Movement modifiers (multiply base tile cost)
    float stairCostMultiplier   = 1.0f;   // WheelchairUser -> set to 1e9 (impassable)
    float rampCostMultiplier    = 1.0f;   // WheelchairUser -> set to 0.5 (preferred)
    float wallAdjacentBonus     = 1.0f;   // Blind          -> set to 0.7 (wall-following)
    float tactilePavingBonus    = 1.0f;   // Blind          -> set to 0.5 (strongly preferred)
    float crowdCostMultiplier   = 1.0f;   // CognitiveDifficulty -> set to 2.0 (stressful)

    // Sensing radius (in grid tiles). Used by Member 1's local memory layer.
    int sightRadius  = 5;   // Blind -> set to 1
    int hearingRange = 5;   // Deaf  -> set to 0

    // Fatigue / cognitive load (0.0 = fresh, 1.0 = exhausted)
    // GOAP uses this to raise action costs dynamically
    float fatigue       = 0.0f;
    float cognitiveLoad = 0.0f;

    // --- Factory helpers -------------------------------------------------
    static AgentProfile MakeBlind() {
        AgentProfile p;
        p.disability         = DisabilityType::Blind;
        p.sightRadius        = 1;
        p.wallAdjacentBonus  = 0.7f;
        p.tactilePavingBonus = 0.5f;
        return p;
    }

    static AgentProfile MakeWheelchairUser() {
        AgentProfile p;
        p.disability           = DisabilityType::WheelchairUser;
        p.stairCostMultiplier  = 1e9f;  // effectively impassable
        p.rampCostMultiplier   = 0.5f;  // ramps are preferred
        return p;
    }

    static AgentProfile MakeDeaf() {
        AgentProfile p;
        p.disability    = DisabilityType::Deaf;
        p.hearingRange  = 0;
        return p;
    }

    static AgentProfile MakeCognitiveDifficulty() {
        AgentProfile p;
        p.disability          = DisabilityType::CognitiveDifficulty;
        p.cognitiveLoad       = 0.3f;   // starts partially loaded
        p.crowdCostMultiplier = 2.0f;
        return p;
    }
};

// ---------------------------------------------------------------------------
// WorldState
// A flat map of named boolean facts. Cheap to copy (used heavily during
// the GOAP planner's internal search).
//
// Convention: fact names are lowercase_snake_case strings.
// Examples:  "at_store", "has_path", "ramp_nearby", "is_blocked"
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
// Updated every simulation tick. Call PopulateFacts() before each Plan()
// call so the GOAP planner gets an up-to-date boolean snapshot.
// ---------------------------------------------------------------------------
struct Blackboard {
    // Survival metrics
    float hungerLevel  = 100.0f;  // 100 = full, 0 = starving
    float fatigueLevel = 0.0f;    // 0 = rested, 100 = exhausted

    // Inventory
    int carriedWood = 0;
    int carriedFood = 0;
    int maxWoodCapacity = 5;
    int maxFoodCapacity = 5;

    // Drain rates — set from AgentProfile (wheelchair user drains fatigue 2x)
    float hungerDrainRate  = 1.0f;
    float fatigueDrainRate = 1.0f;

    // Convenience checks
    bool IsStarving()          const { return hungerLevel  <= 0.0f;  }
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
    // Call this once per tick before calling GOAPPlanner::Plan().
    // Thresholds: hunger critical below 20, fatigue high above 80.
    void PopulateFacts(WorldState& state) const {
        state.Set("hunger_critical",  hungerLevel  <= 20.0f);
        state.Set("fatigue_high",     fatigueLevel >= 80.0f);
        state.Set("has_wood",         carriedWood  >  0);
        state.Set("has_food",         carriedFood  >  0);
        state.Set("inventory_full",   IsInventoryFullOfWood());
    }
};
