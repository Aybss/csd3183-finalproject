// AgentActions.h
// Actions available to every agent in the survival simulation. Every agent
// (WheelchairBound / Blind / Deaf) gets the same action set and the same
// goal priorities — only cost, procedural preconditions, and terrain
// constraints differ by role (see WorldState.h's AgentProfile factories and
// PluginMain.cpp's goal arbitration). That's what makes an impairment
// "affect" an agent instead of locking them out of a task entirely.
//
// `code` on each action matches the AgentAction enum mirrored in
// Assets/Scripts/Agent/GOAP/AgentAction.cs — PlanNextAction() in
// PluginMain.cpp returns this code for the first step of the chosen plan.

#pragma once
#include "goap/Action.h"

enum ActionCode {
    ACT_ExploreUnknown = 0,
    ACT_NavigateToWood = 1,
    ACT_CollectWood = 2,
    ACT_NavigateToFood = 3,
    ACT_Eat = 4,
    ACT_DetectFoodBySound = 5,
    ACT_NavigateToStone = 6,
    ACT_MineStone = 7,
    ACT_NavigateToWater = 8,
    ACT_DrinkWater = 9,
    ACT_NavigateToBuildSite = 10,
    ACT_DeliverWood = 11,
    ACT_DeliverStone = 12,
    ACT_NavigateToCamp = 13,
    ACT_Rest = 14,
};

// ── SHARED SURVIVAL ─────────────────────────────────────────────────────────

// Fallback — wander toward unknown tiles to build up the memory map.
// The more each agent explores, the more useful their SLAM merge becomes.
class ExploreUnknown : public Action {
public:
    ExploreUnknown() {
        name = "ExploreUnknown";
        code = ACT_ExploreUnknown;
        baseCost = 1.0f;
        effects.Set("map_updated", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        float blindPenalty = (profile.disability == DisabilityType::Blind) ? 2.5f : 1.0f;
        return baseCost * blindPenalty * profile.movementCostMultiplier;
    }
};

class Eat : public Action {
public:
    Eat() {
        name = "Eat";
        code = ACT_Eat;
        baseCost = 0.1f;
        preconditions.Set("at_food", true);
        effects.Set("hunger_critical", false);
        effects.Set("at_food", false);
    }
};

class NavigateToWater : public Action {
public:
    NavigateToWater() {
        name = "NavigateToWater";
        code = ACT_NavigateToWater;
        baseCost = 1.0f;
        preconditions.Set("water_location_known", true);
        effects.Set("at_water", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * profile.movementCostMultiplier;
    }
};

class DrinkWater : public Action {
public:
    DrinkWater() {
        name = "DrinkWater";
        code = ACT_DrinkWater;
        baseCost = 0.1f;
        preconditions.Set("at_water", true);
        effects.Set("thirst_critical", false);
    }
};

class Rest : public Action {
public:
    Rest() {
        name = "Rest";
        code = ACT_Rest;
        baseCost = 0.5f;
        preconditions.Set("at_camp", true);
        effects.Set("fatigue_high", false);
    }
};

class NavigateToCamp : public Action {
public:
    NavigateToCamp() {
        name = "NavigateToCamp";
        code = ACT_NavigateToCamp;
        baseCost = 1.0f;
        effects.Set("at_camp", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * profile.movementCostMultiplier;
    }
};

// ── FOOD ─────────────────────────────────────────────────────────────────

// Stop and listen for food — the only way a Deaf agent's hearingRange==0
// keeps this permanently uneconomical (see GetCost), while Blind agents
// (hearingRange 8, sightRadius 1) lean on it heavily.
class DetectFoodBySound : public Action {
public:
    DetectFoodBySound() {
        name = "DetectFoodBySound";
        code = ACT_DetectFoodBySound;
        baseCost = 0.5f;
        effects.Set("food_location_known", true);
    }

    // Deaf agents (hearingRange 0) can't use this at all — a huge cost isn't
    // enough, since the planner would still "find" a technically-valid plan
    // through it when it's the only action that satisfies food_location_known.
    bool CheckProceduralPrecondition(const WorldState& current, const AgentProfile& profile) const override {
        return profile.hearingRange > 0;
    }
};

class NavigateToFood : public Action {
public:
    NavigateToFood() {
        name = "NavigateToFood";
        code = ACT_NavigateToFood;
        baseCost = 1.0f;
        preconditions.Set("food_location_known", true);
        effects.Set("at_food", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * profile.movementCostMultiplier;
    }
};

// ── WOOD ─────────────────────────────────────────────────────────────────

class NavigateToWood : public Action {
public:
    NavigateToWood() {
        name = "NavigateToWood";
        code = ACT_NavigateToWood;
        baseCost = 1.0f;
        preconditions.Set("wood_location_known", true);
        effects.Set("at_wood", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * profile.movementCostMultiplier;
    }
};

class CollectWood : public Action {
public:
    CollectWood() {
        name = "CollectWood";
        code = ACT_CollectWood;
        baseCost = 0.3f;
        preconditions.Set("at_wood", true);
        preconditions.Set("wood_full", false);
        effects.Set("has_wood", true);
        effects.Set("wood_full", true);
        effects.Set("at_wood", false);
    }
};

// ── STONE ────────────────────────────────────────────────────────────────

class NavigateToStone : public Action {
public:
    NavigateToStone() {
        name = "NavigateToStone";
        code = ACT_NavigateToStone;
        baseCost = 1.0f;
        preconditions.Set("stone_location_known", true);
        effects.Set("at_stone", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * profile.movementCostMultiplier;
    }
};

class MineStone : public Action {
public:
    MineStone() {
        name = "MineStone";
        code = ACT_MineStone;
        baseCost = 0.5f;
        preconditions.Set("at_stone", true);
        preconditions.Set("stone_full", false);
        effects.Set("has_stone", true);
        effects.Set("stone_full", true);
        effects.Set("at_stone", false);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        // Mining is physically demanding; wheelchair users take longer per swing.
        float wheelchairPenalty = (profile.disability == DisabilityType::WheelchairUser) ? 1.5f : 1.0f;
        return baseCost * wheelchairPenalty;
    }
};

// ── DELIVERY ─────────────────────────────────────────────────────────────

class NavigateToBuildSite : public Action {
public:
    NavigateToBuildSite() {
        name = "NavigateToBuildSite";
        code = ACT_NavigateToBuildSite;
        baseCost = 1.0f;
        effects.Set("at_build_site", true);
    }

    float GetCost(const AgentProfile& profile, const WorldState& current) const override {
        return baseCost * profile.movementCostMultiplier;
    }
};

class DeliverWood : public Action {
public:
    DeliverWood() {
        name = "DeliverWood";
        code = ACT_DeliverWood;
        baseCost = 0.1f;
        preconditions.Set("has_wood", true);
        preconditions.Set("at_build_site", true);
        effects.Set("has_wood", false);
        effects.Set("wood_full", false);
    }
};

class DeliverStone : public Action {
public:
    DeliverStone() {
        name = "DeliverStone";
        code = ACT_DeliverStone;
        baseCost = 0.1f;
        preconditions.Set("has_stone", true);
        preconditions.Set("at_build_site", true);
        effects.Set("has_stone", false);
        effects.Set("stone_full", false);
    }
};
