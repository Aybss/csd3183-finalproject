// AgentActions.h
// Role-based GOAP actions for the maze house-building simulation.
//
// Each agent only receives actions for their role (set in PluginAPI):
//   Blind (Forager):          ExploreUnknown, DetectFoodBySound, NavigateToFood, Eat
//   Deaf (Lumber Collector):  ExploreUnknown, NavigateToWood, CollectWood,
//                             NavigateToBuildSite, DeliverWood, Eat
//   WheelchairUser (Builder): Rest, Eat, Build
//
//
//   "food_location_known"   — knows where food is (audio or SLAM merge)
//   "at_food"               — standing on food tile (set by PluginAPI)
//   "hunger_resolved"       — just ate
//   "wood_location_known"   — knows where wood is (sight or SLAM merge)
//   "at_wood"               — standing on wood tile (set by PluginAPI)
//   "has_wood"              — carrying wood
//   "at_build_site"         — standing on build site (set by PluginAPI)
//   "wood_delivered"        — wood dropped at build site
//   "house_built"           — win condition (set by PluginAPI when threshold met)
//   "fatigue_resolved"      — just rested
//   "map_updated"           — explored an unknown tile

#pragma once
#include "goap/Action.h"

// ── SHARED ───────────────────────────────────────────────────────────────────

// Fallback — wander toward unknown tiles to build up the memory map.
// The more each agent explores, the more useful their SLAM merge becomes.
class ExploreUnknown : public Action {
public:
    ExploreUnknown() {
        name     = "ExploreUnknown";
        baseCost = 1.0f;
        effects.Set("map_updated", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // Blind agents explore slowly — sight radius 1 means bumping into things
        float blindPenalty = (profile.disability == DisabilityType::Blind) ? 3.0f : 1.0f;
        // Wheelchair users drain fatigue faster per tile
        float movePenalty  = (profile.disability == DisabilityType::WheelchairUser) ? 2.0f : 1.0f;
        return baseCost * blindPenalty * movePenalty * (1.0f + profile.fatigue);
    }
};

// Eat food to resolve hunger. Available to all agents.
class Eat : public Action {
public:
    Eat() {
        name     = "Eat";
        baseCost = 0.1f;
        preconditions.Set("at_food", true);
        effects.Set("hunger_resolved", true);
        effects.Set("at_food",         false);
    }
};

// ── BLIND AGENT (FORAGER) ─────────────────────────────────────────────────────

// Detect food by audio — only the blind agent has hearingRange > 0.
// Deaf agent can never use this. Location is shared to teammates via SLAM merge.
class DetectFoodBySound : public Action {
public:
    DetectFoodBySound() {
        name     = "DetectFoodBySound";
        baseCost = 0.5f;
        // No preconditions — blind agent can always stop and listen
        effects.Set("food_location_known", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // Guard: deaf agent should never have this action but block it if they do
        if (profile.hearingRange == 0) return 1e9f;
        return baseCost;
    }
};

// Navigate to a known food tile.
class NavigateToFood : public Action {
public:
    NavigateToFood() {
        name     = "NavigateToFood";
        baseCost = 1.0f;
        preconditions.Set("food_location_known", true);
        effects.Set("at_food", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        float movePenalty = (profile.disability == DisabilityType::WheelchairUser) ? 2.0f : 1.0f;
        return baseCost * movePenalty * (1.0f + profile.fatigue);
    }
};

// ── DEAF AGENT (LUMBER COLLECTOR) ─────────────────────────────────────────────

// Navigate to a known wood tile.
class NavigateToWood : public Action {
public:
    NavigateToWood() {
        name     = "NavigateToWood";
        baseCost = 1.0f;
        preconditions.Set("wood_location_known", true);
        effects.Set("at_wood", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * (1.0f + profile.fatigue);
    }
};

// Pick up wood from current tile.
class CollectWood : public Action {
public:
    CollectWood() {
        name     = "CollectWood";
        baseCost = 0.3f;
        preconditions.Set("at_wood", true);
        effects.Set("has_wood", true);
        effects.Set("at_wood",  false);
    }
};

// Walk to the build site carrying collected wood.
// Build site location is fixed at spawn so no precondition needed.
class NavigateToBuildSite : public Action {
public:
    NavigateToBuildSite() {
        name     = "NavigateToBuildSite";
        baseCost = 1.0f;
        effects.Set("at_build_site", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * (1.0f + profile.fatigue);
    }
};

// Drop wood at the build site for the wheelchair user to build with.
class DeliverWood : public Action {
public:
    DeliverWood() {
        name     = "DeliverWood";
        baseCost = 0.1f;
        preconditions.Set("has_wood",      true);
        preconditions.Set("at_build_site", true);
        effects.Set("wood_delivered", true);
        effects.Set("has_wood",       false);
    }
};

// ── WHEELCHAIR USER (BUILDER) ─────────────────────────────────────────────────

// Rest in place to recover fatigue.
// Wheelchair user drains fatigue 2x per tile so they need this frequently.
class Rest : public Action {
public:
    Rest() {
        name     = "Rest";
        baseCost = 0.5f;
        effects.Set("fatigue_resolved", true);
    }
};

// Build the house using wood delivered by the deaf agent.
// PluginAPI sets "house_built" externally when enough wood has been processed.
class Build : public Action {
public:
    Build() {
        name     = "Build";
        baseCost = 0.8f;
        preconditions.Set("wood_delivered", true);
        preconditions.Set("at_build_site",  true);
        effects.Set("house_built", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // Cognitive load makes sustained construction harder
        return baseCost * (1.0f + profile.cognitiveLoad);
    }
};
