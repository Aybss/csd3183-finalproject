// api/PluginAPI.cpp
#include "spatial/WorldGrid.h"
//#include "navigation/AStarPathfinder.h"
#include "goap/WorldState.h"
#include "spatial/AgentMemory.h"
#include "spatial/Perception.h" // Needed for UpdatePhysicalSenses

#define EXPORT_API extern "C" __declspec(dllexport)

static WorldGrid g_WorldGrid;

// Keep three distinct mental maps
static AgentMemory g_ForagerMemory;    // ID 0 (Blind)
static AgentMemory g_LumberjackMemory; // ID 1 (Deaf)
static AgentMemory g_BuilderMemory;    // ID 2 (Wheelchair)

// Helper to grab the correct memory reference based on an ID
AgentMemory& GetMemoryByID(int agentID) {
    if (agentID == 0) return g_ForagerMemory;
    if (agentID == 1) return g_LumberjackMemory;
    return g_BuilderMemory;
}

// --------------------------------------------------------
// 1. INITIALIZATION & PROCEDURAL GENERATION SETTERS
// --------------------------------------------------------
EXPORT_API void InitializeGrid(int width, int height) {
    g_WorldGrid.width = width;
    g_WorldGrid.height = height;

    // Allocate Phase 3 Resource & Structure Layers
    g_WorldGrid.wallLayer.resize(width, height, false);
    g_WorldGrid.foodLayer.resize(width, height, 0);
    g_WorldGrid.woodLayer.resize(width, height, 0);

    // Initialize individual fog-of-war maps for the agents
    g_ForagerMemory.initialize(width, height);
    g_LumberjackMemory.initialize(width, height);
    g_BuilderMemory.initialize(width, height);
}

EXPORT_API void SetWallData(bool* flatWallArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for (int i = 0; i < totalCells; ++i) g_WorldGrid.wallLayer[i] = flatWallArray[i];
}

EXPORT_API void SetFoodData(int* flatFoodArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for (int i = 0; i < totalCells; ++i) g_WorldGrid.foodLayer[i] = flatFoodArray[i];
}

EXPORT_API void SetWoodData(int* flatWoodArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for (int i = 0; i < totalCells; ++i) g_WorldGrid.woodLayer[i] = flatWoodArray[i];
}

EXPORT_API void SetBuildSite(int row, int col) {
    g_WorldGrid.buildSiteRow = row;
    g_WorldGrid.buildSiteCol = col;
}

// --------------------------------------------------------
// 2. PATHFINDING (Using Local Memory, NOT Global Grid!)
// --------------------------------------------------------
// Unity asks for a path, C++ fills the outBuffer array
//EXPORT_API int RequestPath(int agentID, int startR, int startC, int endR, int endC, int* outBuffer) {
//    AgentMemory& memory = GetMemoryByID(agentID);
//
//    // Member 2 (Pathfinder) MUST update their AStarPathfinder.h signature 
//    // to accept 'AgentMemory' instead of 'WorldGrid' to respect Fog of War!
//    std::vector<GridPos> path = AStarPathfinder::CalculatePath(memory, startR, startC, endR, endC);
//
//    // Flatten the GridPos path into a 1D int array for Unity [r1, c1, r2, c2...]
//    int bufferIndex = 0;
//    for (const auto& node : path) {
//        outBuffer[bufferIndex++] = node.row;
//        outBuffer[bufferIndex++] = node.col;
//    }
//
//    return static_cast<int>(path.size());
//}

// --------------------------------------------------------
// 3. SLAM SYNC & PERCEPTION 
// --------------------------------------------------------
// Unity calls this when two agents' communication radii overlap
EXPORT_API void TriggerSLAMSync(int agentA_ID, int agentB_ID) {
    AgentMemory& memA = GetMemoryByID(agentA_ID);
    AgentMemory& memB = GetMemoryByID(agentB_ID);

    memA.Merge(memB);
    memB.Merge(memA);
}

// Unity calls this every frame for each agent to clear fog of war
EXPORT_API void AgentPerceive(int agentID, int row, int col, int sightRadius) {
    AgentMemory& mem = GetMemoryByID(agentID);

    AgentProfile tempProfile;
    tempProfile.sightRadius = sightRadius;

    UpdatePhysicalSenses(row, col, g_WorldGrid, mem, tempProfile);
}