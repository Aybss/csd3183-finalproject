#pragma once
#include "MapLayer.h"

struct AgentMemory {
    // Tracks if an agent has physically or visually interacted with a cell
    MapLayer<bool> exploredCells;
    
    // The agent's local mental model of the layout
    MapLayer<bool> discoveredWalls;

    void initialize(int w, int h) {
        exploredCells.resize(w, h, false);
        discoveredWalls.resize(w, h, false); // Initial state assumes all tiles are walkable
    }

    void discover_cell(int row, int col, bool is_wall) {
        exploredCells.at(row, col) = true;
        discoveredWalls.at(row, col) = is_wall;
    }
};