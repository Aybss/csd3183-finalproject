// AgentActions.h
//
// Concrete GOAP actions for the urban accessibility simulation.
// Each action models one step an agent can take in the city environment.

#pragma once
#include "goap/Action.h"           
#include "core/AgentProfile.h"     

// Walk along a known path to the destination.
// Requires a path to already be established (has_path = true).
class WalkToDestination : public Action {
public:
    WalkToDestination() {
        name     = "WalkToDestination";
        baseCost = 1.0f;
        preconditions.Set("has_path", true);
        effects.Set("at_destination", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        float cost = baseCost;
        cost *= (1.0f + profile.fatigue);
        // Crowds are stressful for cognitively challenged agents
        if (current.Get("crowd_present"))
            cost *= profile.crowdCostMultiplier;
        return cost;
    }
};

// Use a ramp to change level (preferred route for wheelchair users).
class UseRamp : public Action {
public:
    UseRamp() {
        name     = "UseRamp";
        baseCost = 1.2f;
        preconditions.Set("ramp_nearby", true);
        effects.Set("level_changed", true);
        effects.Set("used_accessible_route", true);
        effects.Set("has_path", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // WheelchairUser has rampCostMultiplier = 0.5 → ramps are strongly preferred
        return baseCost * profile.rampCostMultiplier * (1.0f + profile.fatigue);
    }
};

// Use stairs to change level (effectively impassable for wheelchair users).
class UseStairs : public Action {
public:
    UseStairs() {
        name     = "UseStairs";
        baseCost = 1.0f;
        preconditions.Set("stairs_nearby", true);
        effects.Set("level_changed", true);
        effects.Set("has_path", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // WheelchairUser has stairCostMultiplier = 1e9 → planner will never pick this
        return baseCost * profile.stairCostMultiplier;
    }
};

// Follow tactile paving strips to navigate without sight.
// Especially beneficial for blind agents (tactilePavingBonus = 0.5).
class FollowTactilePath : public Action {
public:
    FollowTactilePath() {
        name     = "FollowTactilePath";
        baseCost = 0.8f;
        preconditions.Set("tactile_paving_detected", true);
        effects.Set("has_path", true);
        effects.Set("navigation_assisted", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // Blind agents strongly prefer tactile paths (bonus < 1.0 lowers cost)
        return baseCost * profile.tactilePavingBonus * (1.0f + profile.fatigue);
    }
};

// Pause and survey the immediate surroundings to discover nearby features.
// Always available (no preconditions). Populates fact flags that unlock
// other actions (UseRamp, UseStairs, FollowTactilePath).
class ScanEnvironment : public Action {
public:
    ScanEnvironment() {
        name     = "ScanEnvironment";
        baseCost = 0.5f;
        // No preconditions — the agent can always stop and look around.
        effects.Set("ramp_nearby",             true);
        effects.Set("stairs_nearby",           true);
        effects.Set("tactile_paving_detected", true);
        effects.Set("crosswalk_detected",      true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // Blind agents scan a much smaller radius so it takes relatively longer
        float scanPenalty = (profile.disability == DisabilityType::Blind) ? 2.0f : 1.0f;
        // Cognitive load makes processing the environment harder
        return baseCost * scanPenalty * (1.0f + profile.cognitiveLoad);
    }
};
