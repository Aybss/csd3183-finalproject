// navigation/AStarPathfinder.h
#pragma once
#include <vector>
#include "spatial/AgentMemory.h" // NEW: Replaced WorldGrid

struct GridPos {
    int row;
    int col;
};

class AStarPathfinder {
public:
    // NEW: Function signature now takes AgentMemory instead of WorldGrid and Profile
    static std::vector<GridPos> CalculatePath(
        const AgentMemory& memory,
        int startRow, int startCol,
        int endRow, int endCol);
};