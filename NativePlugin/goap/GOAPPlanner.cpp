// GOAPPlanner.cpp

#include "GOAPPlanner.h"
#include <queue>
#include <vector>

// Internal node for the A* search over WorldState space.
struct SearchNode {
    WorldState             state;
    float                  gCost = 0.0f;   // cost accumulated so far
    float                  fCost = 0.0f;   // g + heuristic
    std::vector<const Action*> plan;        // actions taken to reach this state

    bool operator>(const SearchNode& other) const { return fCost > other.fCost; }
};

PlanResult GOAPPlanner::Plan(
    const WorldState&                           start,
    const WorldState&                           goal,
    const std::vector<std::unique_ptr<Action>>& actions,
    const AgentProfile&                         profile)
{
    std::priority_queue<SearchNode,
                        std::vector<SearchNode>,
                        std::greater<SearchNode>> open;

    SearchNode initial;
    initial.state = start;
    initial.gCost = 0.0f;
    initial.fCost = static_cast<float>(Heuristic(start, goal));
    open.push(std::move(initial));

    // Guard against runaway search in complex or unsolvable worlds.
    const int MAX_ITERATIONS = 1000;
    int iterations = 0;

    while (!open.empty() && iterations++ < MAX_ITERATIONS) {
        SearchNode current = open.top();
        open.pop();

        if (current.state.Satisfies(goal)) {
            return { true, current.plan, current.gCost };
        }

        for (const auto& action : actions) {
            if (!action->IsApplicable(current.state, profile)) continue;

            float      actionCost = action->GetCost(profile, current.state);
            WorldState nextState  = current.state.ApplyEffects(action->GetEffects());

            SearchNode next;
            next.state = std::move(nextState);
            next.gCost = current.gCost + actionCost;
            next.fCost = next.gCost + static_cast<float>(Heuristic(next.state, goal));
            next.plan  = current.plan;
            next.plan.push_back(action.get());

            open.push(std::move(next));
        }
    }

    return { false, {}, 0.0f };  // no plan found within iteration budget
}

int GOAPPlanner::Heuristic(const WorldState& current, const WorldState& goal) const {
    int unsatisfied = 0;
    for (const auto& [key, value] : goal.facts) {
        if (current.Get(key) != value) ++unsatisfied;
    }
    return unsatisfied;
}
