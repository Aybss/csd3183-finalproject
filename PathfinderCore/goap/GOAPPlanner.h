// GOAPPlanner.h
//
// A* forward-search planner over WorldState space.
// Given a start state, a goal state, and a set of available actions,
// it returns the lowest-cost action sequence that satisfies the goal.

#pragma once
#include "Action.h"
#include <vector>
#include <memory>

struct PlanResult {
    bool success = false;
    std::vector<const Action*> actions;  // ordered sequence to execute
    float totalCost = 0.0f;
};

class GOAPPlanner {
public:
    // Plan from 'start' toward 'goal' using the given action set.
    // profile is forwarded to each action's cost and procedural-precondition
    // checks so the plan is impairment-aware.
    PlanResult Plan(
        const WorldState& start,
        const WorldState& goal,
        const std::vector<std::unique_ptr<Action>>& actions,
        const AgentProfile& profile
    );

private:
    // Heuristic: count of goal facts not yet satisfied in 'current'.
    int Heuristic(const WorldState& current, const WorldState& goal) const;
};
