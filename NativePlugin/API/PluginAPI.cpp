// PluginAPI.cpp
#include "spatial/WorldGrid.h"
#include "navigation/AStarPathfinder.h"
#include "goap/WorldState.h"

#define EXPORT_API extern "C" __declspec(dllexport)

// The single global instance of your map living in C++ memory
static WorldGrid g_WorldGrid;

EXPORT_API void InitializeGrid(int width, int height) {
    g_WorldGrid.width = width;
    g_WorldGrid.height = height;
    
    // Initialize layers using your MapLayer.h functions
    g_WorldGrid.wallLayer.resize(width, height, false);
    g_WorldGrid.stairLayer.resize(width, height, false);
    g_WorldGrid.rampLayer.resize(width, height, 0.0f);
}

// Unity passes a flat bool array, C++ copies it into Member 1's spatial grid
EXPORT_API void SetWallData(bool* flatWallArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for(int i = 0; i < totalCells; ++i) {
        g_WorldGrid.wallLayer[i] = flatWallArray[i];
    }
}

// Unity asks for a path, C++ fills the outBuffer array
EXPORT_API int RequestPath(int startR, int startC, int endR, int endC, int profileType, int* outBuffer) {
    AgentProfile profile;
    if (profileType == 1) profile = AgentProfile::CreateWheelchairProfile();
    else if (profileType == 2) profile = AgentProfile::CreateBlindProfile();

    std::vector<GridPos> path = AStarPathfinder::CalculatePath(g_WorldGrid, profile, startR, startC, endR, endC);
    
    // Flatten the GridPos path into a 1D int array for Unity [r1, c1, r2, c2...]
    int bufferIndex = 0;
    for(const auto& node : path) {
        outBuffer[bufferIndex++] = node.row;
        outBuffer[bufferIndex++] = node.col;
    }
    
    return static_cast<int>(path.size()); // Tell Unity how many steps are in the path
}