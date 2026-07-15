// goap/Action.h
//
// Abstract base class for all GOAP actions.
// Each concrete action defines its preconditions, effects, and cost.

#pragma once
#include "WorldState.h"
#include <string>

class Action {
public:
    virtual ~Action() = default;

    const std::string& GetName() const { return name; }
    const WorldState&  GetPreconditions() const { return preconditions; }
    const WorldState&  GetEffects()       const { return effects; }
    int GetCode() const { return code; }

    // Base cost, optionally overridden to apply profile modifiers.
    virtual float GetCost(const AgentProfile& profile, const WorldState& current) const {
        return baseCost;
    }

    // Optional runtime check beyond static fact matching.
    virtual bool CheckProceduralPrecondition(const WorldState& current,
                                             const AgentProfile& profile) const {
        return true;
    }

    // Combines static precondition check and procedural check.
    bool IsApplicable(const WorldState& current, const AgentProfile& profile) const {
        return current.Satisfies(preconditions)
            && CheckProceduralPrecondition(current, profile);
    }

protected:
    std::string name;
    int         code = -1; // Matches the AgentAction enum in C# (AgentAction.cs)
    WorldState  preconditions;
    WorldState  effects;
    float       baseCost = 1.0f;
};
