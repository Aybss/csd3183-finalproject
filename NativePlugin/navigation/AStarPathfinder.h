// AStarPathfinder.h
#pragma once
#include "spatial/WorldGrid.h"
#include "core/AgentProfile.h"
#include <vector>

// Simple struct to hold coordinate results
struct GridPos {
    int row;
    int col;
};

class AStarPathfinder {
public:
    // Takes the environment, the agent's profile, and start/end points
    // Returns a sequence of grid coordinates forming the optimal path
    static std::vector<GridPos> CalculatePath(
        const WorldGrid& grid, 
        const AgentProfile& profile, 
        int startRow, int startCol, 
        int endRow, int endCol
    );

private:
    // Member 2 will implement the heuristic and cost logic in a .cpp file
    static float CalculateNodeCost(const WorldGrid& grid, const AgentProfile& profile, int row, int col);
};