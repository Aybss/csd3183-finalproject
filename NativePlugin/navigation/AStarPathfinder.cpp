// navigation/AStarPathfinder.cpp
#include "navigation/AStarPathfinder.h"

// NEW: Matching signature
std::vector<GridPos> AStarPathfinder::CalculatePath(
    const AgentMemory& memory,
    int startRow, int startCol,
    int endRow, int endCol)
{
    std::vector<GridPos> dummyPath;

    // Just return a straight line from start to end to prevent crashes during testing
    dummyPath.push_back({ startRow, startCol });
    dummyPath.push_back({ endRow, endCol });

    return dummyPath;
}