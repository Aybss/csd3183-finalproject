#pragma once
#include "spatial/MapLayer.h" 

struct WorldGrid {
    int width = 0;
    int height = 0;

    // Environmental features
    MapLayer<bool>  wallLayer;          // true = impassable structure/building
    MapLayer<bool>  stairLayer;         // true = vertical physical stairs
    MapLayer<float> rampLayer;          // float value representing incline grade percentage
    MapLayer<bool>  tactilePaving;      // true = explicit guiding paths painted on floor
    MapLayer<float> audioChimeVolume;   // float representing volume propagation from audio crosswalk chirpers

    MapLayer<bool> audioBeaconLayer;   // Emits sound for crosswalks
    MapLayer<int> crowdDensityLayer;   // Tracks how many people are in a cell (0 = empty, >0 = crowded)

};