// spatial/AgentMemory.h
//
// A single agent's SLAM fog-of-war state: which tiles it has personally
// explored (or heard about), and what it remembers being there. Two agents
// sync via Merge() when they pass near each other (see TriggerSLAMSync in
// PluginMain.cpp) — that's the "group" half of SLAM: knowledge spreads
// through the team without a shared omniscient map.
#pragma once
#include "spatial/MapLayer.h"

struct AgentMemory {
    MapLayer<bool> exploredTiles;     // Cleared fog-of-war (seen or heard)
    MapLayer<bool> discoveredWalls;
    MapLayer<bool> discoveredWood;
    MapLayer<bool> discoveredFood;
    MapLayer<bool> discoveredStone;
    MapLayer<bool> discoveredWaterEdge;

    int buildSiteX = -1;
    int buildSiteY = -1;
    int campX = -1;
    int campY = -1;

    void initialize(int width, int height) {
        exploredTiles.resize(width, height, false);
        discoveredWalls.resize(width, height, false);
        discoveredWood.resize(width, height, false);
        discoveredFood.resize(width, height, false);
        discoveredStone.resize(width, height, false);
        discoveredWaterEdge.resize(width, height, false);
        buildSiteX = -1;
        buildSiteY = -1;
        campX = -1;
        campY = -1;
    }

    // SLAM Sync: bitwise merge of another agent's knowledge into this one.
    // Every accumulated layer (everything except discoveredWalls, which
    // trusts the other agent's latest read outright) uses |= rather than
    // logical-OR — a plain bitwise-OR-assignment against each bool's 0/1
    // representation, since both operands are already-evaluated reads with
    // no short-circuit-relevant side effects.
    void Merge(const AgentMemory& other) {
        int totalCells = exploredTiles.get_width() * exploredTiles.get_height();
        for (int i = 0; i < totalCells; ++i) {
            if (other.exploredTiles[i]) {
                exploredTiles[i] |= other.exploredTiles[i];
                discoveredWalls[i] = other.discoveredWalls[i];
                discoveredWood[i] |= other.discoveredWood[i];
                discoveredFood[i] |= other.discoveredFood[i];
                discoveredStone[i] |= other.discoveredStone[i];
                discoveredWaterEdge[i] |= other.discoveredWaterEdge[i];
            }
        }

        if (buildSiteX == -1 && other.buildSiteX != -1) {
            buildSiteX = other.buildSiteX;
            buildSiteY = other.buildSiteY;
        }
        if (campX == -1 && other.campX != -1) {
            campX = other.campX;
            campY = other.campY;
        }
    }
};
