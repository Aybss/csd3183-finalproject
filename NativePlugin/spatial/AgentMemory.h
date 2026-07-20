// spatial/AgentMemory.h
#pragma once
#include "spatial/MapLayer.h"

struct AgentMemory {
    // True = Visited/Explored (Cleared Fog of War), False = Unexplored
    MapLayer<bool> exploredTiles;

    // True = Wall, False = Walkable (Only valid if exploredTiles is true)
    MapLayer<bool> discoveredWalls;

    // Remembers quantities of resources left at tiles
    MapLayer<int> discoveredFood;
    MapLayer<int> discoveredWood;

    // Remembers where the home build site is once found
    int buildSiteRow = -1; // -1 means unknown
    int buildSiteCol = -1;

    void initialize(int width, int height) {
        exploredTiles.resize(width, height, false);
        discoveredWalls.resize(width, height, false);
        discoveredFood.resize(width, height, 0);
        discoveredWood.resize(width, height, 0);
        buildSiteRow = -1;
        buildSiteCol = -1;
    }

    // SLAM Sync: per-tile union merge when two agents overlap communication range
    void Merge(const AgentMemory& other) {
        int totalCells = exploredTiles.get_width() * exploredTiles.get_height();
        for (int i = 0; i < totalCells; ++i) {
            // If the other agent has explored a tile we haven't, learn from them
            if (other.exploredTiles[i]) {
                exploredTiles[i] = true;
                discoveredWalls[i] = other.discoveredWalls[i];
                discoveredFood[i] = other.discoveredFood[i];
                discoveredWood[i] = other.discoveredWood[i];
            }
        }

        // Sync build site coordinates if we didn't know them
        if (buildSiteRow == -1 && other.buildSiteRow != -1) {
            buildSiteRow = other.buildSiteRow;
            buildSiteCol = other.buildSiteCol;
        }
    }
};