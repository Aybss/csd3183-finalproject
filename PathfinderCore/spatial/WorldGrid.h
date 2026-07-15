// spatial/WorldGrid.h
//
// Ground truth for the whole map — populated once from Unity's exported
// biome data (see PluginMain.cpp LoadTerrainGrid). Every layer is indexed
// as layer.at(y, x) (row = y, col = x) to match AStarGrid's x + y*width
// flat layout.
#pragma once
#include "spatial/MapLayer.h"

struct WorldGrid {
    int width = 0;
    int height = 0;

    MapLayer<bool> wallLayer;      // true = not walkable
    MapLayer<bool> woodLayer;      // true = choppable wood on this tile
    MapLayer<bool> foodLayer;      // true = food node on this tile
    MapLayer<bool> stoneLayer;     // true = mineable stone on this tile
    MapLayer<bool> waterEdgeLayer; // true = walkable tile adjacent to water (drinkable from here)

    int buildSiteX = -1;
    int buildSiteY = -1;
    int campX = -1;
    int campY = -1;

    void initialize(int w, int h) {
        width = w;
        height = h;
        wallLayer.resize(w, h, false);
        woodLayer.resize(w, h, false);
        foodLayer.resize(w, h, false);
        stoneLayer.resize(w, h, false);
        waterEdgeLayer.resize(w, h, false);
    }
};
