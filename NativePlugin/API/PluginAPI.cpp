// api/PluginAPI.cpp
#include "spatial/WorldGrid.h"
#include "navigation/AStarPathfinder.h"
#include "goap/WorldState.h" // Group mate's file containing the official AgentProfile

#define EXPORT_API extern "C" __declspec(dllexport)

static WorldGrid g_WorldGrid;


EXPORT_API void InitializeGrid(int width, int height) {
    g_WorldGrid.width = width;
    g_WorldGrid.height = height;

    // Existing layers
    g_WorldGrid.wallLayer.resize(width, height, false);
    g_WorldGrid.stairLayer.resize(width, height, false);
    g_WorldGrid.rampLayer.resize(width, height, 0.0f);

    // NEW: Allocate memory for the Phase 2 layers
    g_WorldGrid.audioBeaconLayer.resize(width, height, false);
    g_WorldGrid.crowdDensityLayer.resize(width, height, 0);
}


EXPORT_API void SetWallData(bool* flatWallArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for (int i = 0; i < totalCells; ++i) {
        g_WorldGrid.wallLayer[i] = flatWallArray[i];
    }
}

// Unity asks for a path, C++ fills the outBuffer array
EXPORT_API int RequestPath(int startR, int startC, int endR, int endC, int profileType, int* outBuffer) {
    AgentProfile profile;

    // Map to your group mate's official factory methods and profile IDs
    if (profileType == 1) {
        profile = AgentProfile::MakeWheelchairUser(); 
    }
    else if (profileType == 2) {
        profile = AgentProfile::MakeBlind(); 
    }
    else if (profileType == 3) {
        profile = AgentProfile::MakeDeaf(); 
    }
    else if (profileType == 4) {
        profile = AgentProfile::MakeCognitiveDifficulty(); 
    }

    std::vector<GridPos> path = AStarPathfinder::CalculatePath(g_WorldGrid, profile, startR, startC, endR, endC); 

        // Flatten the GridPos path into a 1D int array for Unity [r1, c1, r2, c2...]
        int bufferIndex = 0;
    for (const auto& node : path) {
        outBuffer[bufferIndex++] = node.row; 
        outBuffer[bufferIndex++] = node.col;
    }

    return static_cast<int>(path.size()); // Tell Unity how many steps are in the path[cite: 4]
}

// NEW: Unity calls this once during map setup to place crosswalk chimes
EXPORT_API void SetAudioBeaconData(bool* flatAudioArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for (int i = 0; i < totalCells; ++i) {
        g_WorldGrid.audioBeaconLayer[i] = flatAudioArray[i];
    }
}

// NEW: Unity calls this EVERY FRAME to update where the moving pedestrians are
EXPORT_API void UpdateCrowdDensity(int* flatCrowdArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for (int i = 0; i < totalCells; ++i) {
        g_WorldGrid.crowdDensityLayer[i] = flatCrowdArray[i];
    }
}