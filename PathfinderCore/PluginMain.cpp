#include "AStarGrid.h"
#include "Agent.h"
#include <vector>
#include <memory>

#pragma pack(push, 1) // Ensures alignment matches C# packing exactly
struct SimpleNodeData
{
    int x;
    int y;
    bool isWalkable;
    float movementCost;
    int biomeType; // Matches C# BiomeType (0 = Grass, 1 = Water, 2 = Wood, 3 = Food)
};
#pragma pack(pop)

static AStarGrid g_grid;
static std::vector<std::unique_ptr<Agent>> g_agents;
static std::vector<SoundCue> g_activeSounds;

extern "C" {

    __declspec(dllexport) int InitializeGrid(int width, int height)
    {
        g_grid.Init(width, height);
        return 1;
    }

    // EXPOSED TO UNITY: Feeds the flat procedural data directly into your AStarGrid
    __declspec(dllexport) void LoadTerrainGrid(SimpleNodeData* gridData, int width, int height)
    {
        if (!gridData) return;

        // 1. Re-initialize AStarGrid bounds natively
        g_grid.Init(width, height);

        // 2. Iterate through the flat sequential memory and update cellular status
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int index = (y * width) + x; // Row-major linear mapping
                const SimpleNodeData& node = gridData[index];

                // Synchronize blocking states with the A* navigation grid
                g_grid.SetBlocked(x, y, !node.isWalkable);

                // Map your customized cell types (biome type index)
                g_grid.SetCellType(x, y, node.biomeType);
            }
        }
    }

    __declspec(dllexport) void SetCellBlocked(int x, int y, int blocked)
    {
        g_grid.SetBlocked(x, y, blocked != 0);
    }

    // SetCellType is used to mark environmental obstacles like stairs (Type 2)
    __declspec(dllexport) void SetCellType(int x, int y, int cellType)
    {
        g_grid.SetCellType(x, y, cellType);
    }

    __declspec(dllexport) void LoadObstacleData(unsigned char* data, int length)
    {
        g_grid.LoadFromBytes(data, length);
    }

    __declspec(dllexport) int FindPath(int startX, int startY, int endX, int endY,
        int* outX, int* outY, int maxPathLength)
    {
        std::vector<PathNode> path = g_grid.FindPath(startX, startY, endX, endY);

        if (path.empty()) return -1;
        if (static_cast<int>(path.size()) > maxPathLength) return -2;

        for (size_t i = 0; i < path.size(); i++)
        {
            outX[i] = path[i].x;
            outY[i] = path[i].y;
        }

        return static_cast<int>(path.size());
    }

    // --- Agent exclusive role functions ---

    // Creates a new Agent assigned to a specific physical constraint role
    __declspec(dllexport) int CreateAgent(int role)
    {
        auto agent = std::make_unique<Agent>();
        agent->Init(&g_grid, static_cast<AgentRole>(role));

        // Recycle empty slots from previously destroyed agents
        for (size_t i = 0; i < g_agents.size(); ++i)
        {
            if (g_agents[i] == nullptr)
            {
                g_agents[i] = std::move(agent);
                return static_cast<int>(i);
            }
        }

        g_agents.push_back(std::move(agent));
        return static_cast<int>(g_agents.size()) - 1;
    }

    __declspec(dllexport) void DestroyAgent(int agentHandle)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size())) return;
        g_agents[agentHandle].reset();
    }

    __declspec(dllexport) void AddSoundCue(float x, float y, float radius, float costPenalty)
    {
        g_activeSounds.push_back({ x, y, radius, costPenalty });
    }

    __declspec(dllexport) void ClearSoundCues()
    {
        g_activeSounds.clear();
    }

    __declspec(dllexport) int FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY,
        int* outX, int* outY, int maxPathLength)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size())) return -1;
        if (!g_agents[agentHandle]) return -1;

        std::vector<PathNode> path = g_agents[agentHandle]->FindPath(startX, startY, endX, endY, g_activeSounds);

        if (path.empty()) return -1;
        if (static_cast<int>(path.size()) > maxPathLength) return -2;

        for (size_t i = 0; i < path.size(); i++)
        {
            outX[i] = path[i].x;
            outY[i] = path[i].y;
        }

        return static_cast<int>(path.size());
    }
}