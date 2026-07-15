// api/PluginAPI.cpp
#include "spatial/WorldGrid.h"
#include "goap/WorldState.h"
#include "spatial/AgentMemory.h"
#include "spatial/Perception.h" // Needed for UpdatePhysicalSenses

// Bring in Member 2's pathfinding headers
#include "navigation/AStarGrid.h"
#include "navigation/Agent.h"

#include <vector>
#include <memory>

#define EXPORT_API extern "C" __declspec(dllexport)

// --------------------------------------------------------
// UNIFIED GLOBAL BACKEND STORAGE
// --------------------------------------------------------
static WorldGrid g_WorldGrid;

// Keep three distinct mental maps for SLAM
static AgentMemory g_ForagerMemory;    // ID 0 (Blind)
static AgentMemory g_LumberjackMemory; // ID 1 (Deaf)
static AgentMemory g_BuilderMemory;    // ID 2 (Wheelchair)

// Member 2's Pathfinding Globals
static AStarGrid g_grid;
static std::vector<std::unique_ptr<Agent>> g_agents;
static std::vector<SoundCue> g_activeSounds;

// Helper to grab the correct memory reference based on an ID
AgentMemory& GetMemoryByID(int agentID) {
    if (agentID == 0) return g_ForagerMemory;
    if (agentID == 1) return g_LumberjackMemory;
    return g_BuilderMemory;
}

// --------------------------------------------------------
// 1. INITIALIZATION & PROCEDURAL GENERATION SETTERS
// --------------------------------------------------------
EXPORT_API int InitializeGrid(int width, int height) {
    // 1. Initialize our unified Spatial/SLAM data
    g_WorldGrid.width = width;
    g_WorldGrid.height = height;

    g_WorldGrid.wallLayer.resize(width, height, false);
    g_WorldGrid.foodLayer.resize(width, height, 0);
    g_WorldGrid.woodLayer.resize(width, height, 0);

    g_ForagerMemory.initialize(width, height);
    g_LumberjackMemory.initialize(width, height);
    g_BuilderMemory.initialize(width, height);

    // 2. Initialize Member 2's pathfinding grid
    g_grid.Init(width, height);
    return 1;
}

EXPORT_API void SetWallData(bool* flatWallArray) {
    int totalCells = g_WorldGrid.width * g_WorldGrid.height;
    for (int i = 0; i < totalCells; ++i) {
        g_WorldGrid.wallLayer[i] = flatWallArray[i];
        
        // Keep Member 2's pathfinding grid in sync with wall configurations!
        g_grid.SetBlocked(i % g_WorldGrid.width, i / g_WorldGrid.width, flatWallArray[i]);
    }
}

EXPORT_API void SetCellBlocked(int x, int y, int blocked) {
    g_grid.SetBlocked(x, y, blocked != 0);
}

EXPORT_API void SetCellType(int x, int y, int cellType) {
    g_grid.SetCellType(x, y, cellType);
}

EXPORT_API void LoadObstacleData(unsigned char* data, int length) {
    g_grid.LoadFromBytes(data, length);
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
// 2. PATHFINDING EXPORTS (Direct Copy from PathfinderCore)
// --------------------------------------------------------
EXPORT_API int FindPath(int startX, int startY, int endX, int endY,
    int* outX, int* outY, int maxPathLength) {
    
    std::vector<PathNode> path = g_grid.FindPath(startX, startY, endX, endY);

    if (path.empty()) return -1;
    if (static_cast<int>(path.size()) > maxPathLength) return -2;

    for (size_t i = 0; i < path.size(); i++) {
        outX[i] = path[i].x;
        outY[i] = path[i].y;
    }

    return static_cast<int>(path.size());
}

// --------------------------------------------------------
// 3. AGENT ACTIONS & SENSORY ROLE METHODS (Direct Copy from PathfinderCore)
// --------------------------------------------------------
EXPORT_API int CreateAgent(int role) {
    auto agent = std::make_unique<Agent>();
    agent->Init(&g_grid, static_cast<AgentRole>(role));

    // Recycle empty slots from previously destroyed agents
    for (size_t i = 0; i < g_agents.size(); ++i) {
        if (g_agents[i] == nullptr) {
            g_agents[i] = std::move(agent);
            return static_cast<int>(i);
        }
    }

    g_agents.push_back(std::move(agent));
    return static_cast<int>(g_agents.size()) - 1;
}

EXPORT_API void DestroyAgent(int agentHandle) {
    if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size())) return;
    g_agents[agentHandle].reset();
}

EXPORT_API void AddSoundCue(float x, float y, float radius, float costPenalty) {
    g_activeSounds.push_back({ x, y, radius, costPenalty });
}

EXPORT_API void ClearSoundCues() {
    g_activeSounds.clear();
}

EXPORT_API int FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY,
    int* outX, int* outY, int maxPathLength) {
    
    if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size()) || !g_agents[agentHandle]) return -1;

    std::vector<PathNode> path = g_agents[agentHandle]->FindPath(startX, startY, endX, endY, g_activeSounds);

    if (path.empty()) return -1;
    if (static_cast<int>(path.size()) > maxPathLength) return -2;

    for (size_t i = 0; i < path.size(); i++) {
        outX[i] = path[i].x;
        outY[i] = path[i].y;
    }

    return static_cast<int>(path.size());
}

// Added to map to the IsAgentCellUnknown P/Invoke inside NativeBridge.cs
EXPORT_API int IsAgentCellUnknown(int agentHandle, int x, int y) {
    // Safety check for valid handles
    if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size())) return 1;

    AgentMemory& mem = GetMemoryByID(agentHandle);

    // Calculate 1D index since MapLayer uses a flat index structure
    int width = mem.exploredTiles.get_width();
    int index = x + y * width;

    // It is "unknown" if exploredTiles is false
    return !mem.exploredTiles[index] ? 1 : 0;
}

EXPORT_API void UpdateAgentVision(int agentHandle, int x, int y) {
    if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size()) || !g_agents[agentHandle]) return;
    g_agents[agentHandle]->UpdateVision(x, y);
}

// --------------------------------------------------------
// 4. SLAM SYNC & PERCEPTION 
// --------------------------------------------------------
EXPORT_API void TriggerSLAMSync(int agentA_ID, int agentB_ID) {
    AgentMemory& memA = GetMemoryByID(agentA_ID);
    AgentMemory& memB = GetMemoryByID(agentB_ID);

    memA.Merge(memB);
    memB.Merge(memA);
}

EXPORT_API void AgentPerceive(int agentID, int row, int col, int sightRadius) {
    AgentMemory& mem = GetMemoryByID(agentID);

    AgentProfile tempProfile;
    tempProfile.sightRadius = sightRadius;

    UpdatePhysicalSenses(row, col, g_WorldGrid, mem, tempProfile);
}