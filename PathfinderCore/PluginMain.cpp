#include "AStarGrid.h"
#include "Agent.h"
#include <vector>
#include <memory>

// A single grid instance shared by all exported functions. Simple and
// fine for now — if you later need multiple independent grids (e.g.
// per-level), swap this for a handle-based system.
static AStarGrid g_grid;

// Agents are created on demand via CreateAgent() and referenced by
// handle (their index in this vector) from then on.
static std::vector<std::unique_ptr<Agent>> g_agents;

// Sound cues currently active in the world. Add per-event via
// AddSoundCue(); clear each tick/frame via ClearSoundCues() if you
// want cues to be transient rather than permanent.
static std::vector<SoundCue> g_activeSounds;

// The extern "C" block prevents C++ from name-mangling these
// functions, so C# can find them by their exact text name via
// [DllImport].
extern "C" {

    __declspec(dllexport) int InitializeGrid(int width, int height)
    {
        g_grid.Init(width, height);
        return 1; // 1 = success
    }

    // blocked: 0 = free, non-zero = blocked. (Using int instead of
    // bool here avoids C#/C++ bool marshaling pitfalls.)
    __declspec(dllexport) void SetCellBlocked(int x, int y, int blocked)
    {
        g_grid.SetBlocked(x, y, blocked != 0);
    }

    // Bulk-loads an occupancy grid. `data` should be a flat byte array
    // matching Unity's ObstacleGrid.ExportOccupancyBytes() output
    // (0 = free, 1 = blocked), and `length` must match width*height
    // from InitializeGrid.
    __declspec(dllexport) void LoadObstacleData(unsigned char* data, int length)
    {
        g_grid.LoadFromBytes(data, length);
    }

    // Finds a path and writes the result into caller-provided arrays.
    // outX/outY must each be pre-allocated by the caller with at
    // least maxPathLength ints.
    //
    // Returns:
    //   >= 0  the number of cells written to outX/outY
    //   -1    no path exists between start and end
    //   -2    a path exists but is longer than maxPathLength
    //         (increase the buffer size and try again)
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

    // --- Agent perception / "disabilities" extension ---

    // Creates a new agent and returns its handle (>= 0) for use with
    // the Agent* functions below. Call InitializeGrid() first.
    __declspec(dllexport) int CreateAgent(int visionRange, int canHear)
    {
        AgentPerception perception;
        perception.visionRange = visionRange;
        perception.canHear = (canHear != 0);

        auto agent = std::make_unique<Agent>();
        agent->Init(&g_grid, perception);

        // Check if there is an empty slot (nullptr) from a previously destroyed agent to reuse
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

    // Destroys an agent by its handle and frees up its memory
    __declspec(dllexport) void DestroyAgent(int agentHandle)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size())) return;
        g_agents[agentHandle].reset(); // Destroys the Agent instance, freeing unmanaged heap memory
    }

    // Reveals the world around (x, y) to the given agent, out to its vision range.
    __declspec(dllexport) void UpdateAgentVision(int agentHandle, int x, int y)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size()) || !g_agents[agentHandle]) return;
        g_agents[agentHandle]->UpdateVision(x, y);
    }

    // Registers a sound event in the world
    __declspec(dllexport) void AddSoundCue(float x, float y, float radius, float costPenalty)
    {
        g_activeSounds.push_back({ x, y, radius, costPenalty });
    }

    // Clears all currently-registered sound cues.
    __declspec(dllexport) void ClearSoundCues()
    {
        g_activeSounds.clear();
    }

    // Plans a path using the given agent's own knowledge
    __declspec(dllexport) int FindAgentPath(int agentHandle, int startX, int startY, int endX, int endY,
        int* outX, int* outY, int maxPathLength)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size()) || !g_agents[agentHandle]) return -1;

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
} // Closes extern "C"