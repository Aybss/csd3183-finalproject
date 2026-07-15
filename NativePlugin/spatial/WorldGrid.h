// spatial/WorldGrid.h
#pragma once
#include "spatial/MapLayer.h"

struct WorldGrid {
    int width = 0;
    int height = 0;

    // The Physical Maze Structure
    MapLayer<bool> wallLayer;

    MapLayer<bool> audioBeaconLayer;   // Emits sound for crosswalks
    MapLayer<int> crowdDensityLayer;   // Tracks how many people are in a cell (0 = empty, >0 = crowded)
    MapLayer<bool> woodLayer;          // true = wood pile on this tile
    MapLayer<bool> foodLayer;          // true = food node on this tile

    // The Goal / Base Building Coordinates
    int buildSiteRow = 0;
    int buildSiteCol = 0;
    int houseCompletion = 0;  // Progress counter (e.g., build goes from 0 to 10)
};