#include <iostream>
#include <vector>
#include <memory>
#include "goap/GOAPPlanner.h"
#include "goap/AgentActions.h"

static std::vector<std::unique_ptr<Action>> MakeActionSet() {
    std::vector<std::unique_ptr<Action>> actions;
    actions.push_back(std::make_unique<ScanEnvironment>());
    actions.push_back(std::make_unique<FollowTactilePath>());
    actions.push_back(std::make_unique<UseRamp>());
    actions.push_back(std::make_unique<UseStairs>());
    actions.push_back(std::make_unique<WalkToDestination>());
    return actions;
}

static void RunTest(const std::string& label, const AgentProfile& profile) {
    std::cout << "=== " << label << " ===\n";

    WorldState start;  // everything unknown/false

    WorldState goal;
    goal.Set("at_destination", true);

    auto actions = MakeActionSet();
    GOAPPlanner planner;
    PlanResult result = planner.Plan(start, goal, actions, profile);

    if (result.success) {
        std::cout << "Plan found (" << result.actions.size()
                  << " steps, cost=" << result.totalCost << "):\n";
        for (size_t i = 0; i < result.actions.size(); ++i)
            std::cout << "  " << (i + 1) << ". " << result.actions[i]->GetName() << "\n";
    } else {
        std::cout << "No plan found.\n";
    }
    std::cout << "\n";
}

int main() {
    RunTest("No disability",        AgentProfile{});
    RunTest("Wheelchair user",      AgentProfile::MakeWheelchairUser());
    RunTest("Blind agent",          AgentProfile::MakeBlind());
    RunTest("Deaf agent",           AgentProfile::MakeDeaf());
    RunTest("Cognitive difficulty", AgentProfile::MakeCognitiveDifficulty());
    return 0;
}
