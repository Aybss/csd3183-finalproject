#include "AStarGrid.h"
#include "Agent.h"
#include "goap/AgentActions.h"
#include "goap/GOAPPlanner.h"
#include "spatial/WorldGrid.h"
#include "spatial/AgentMemory.h"
#include "spatial/Perception.h"
#include "PathNetwork.h"
#include <vector>
#include <memory>
#include <cmath>

#pragma pack(push, 1) // Ensures alignment matches C# packing exactly
struct SimpleNodeData
{
    int x;
    int y;
    bool isWalkable;
    float movementCost;
    int biomeType; // Matches C# BiomeType (0=Grass, 1=Water, 2=Wood, 3=Food, 4=Stone)
};
#pragma pack(pop)

static const int BIOME_GRASS = 0;
static const int BIOME_WATER = 1;
static const int BIOME_WOOD = 2;
static const int BIOME_FOOD = 3;
static const int BIOME_STONE = 4;

// --------------------------------------------------------
// PATHFINDING STATE (unchanged from the original plugin)
// --------------------------------------------------------
static AStarGrid g_grid;
static std::vector<std::unique_ptr<Agent>> g_agents;
static std::vector<SoundCue> g_activeSounds;

// --------------------------------------------------------
// GOAP + SLAM STATE
// One slot per agent handle, parallel to g_agents (grown/recycled the same
// way CreateAgent/DestroyAgent already manage g_agents).
// --------------------------------------------------------
static WorldGrid g_worldGrid;
static std::vector<AgentMemory> g_agentMemories;
static std::vector<AgentProfile> g_agentProfiles;
static std::vector<Blackboard> g_agentBlackboards;
static std::vector<std::unique_ptr<Action>> g_actionSet; // shared, read-only action catalog

// Resource reservation: -1 = unclaimed, otherwise the agent handle that
// claimed this tile. Lets the group split up instead of racing for the same
// tree/stone the moment two agents both discover it via SLAM.
static MapLayer<int> g_woodReservedBy;
static MapLayer<int> g_foodReservedBy;
static MapLayer<int> g_stoneReservedBy;

static void BuildActionSetIfNeeded()
{
    if (!g_actionSet.empty()) return;

    g_actionSet.push_back(std::make_unique<ExploreUnknown>());
    g_actionSet.push_back(std::make_unique<Eat>());
    g_actionSet.push_back(std::make_unique<DetectFoodBySound>());
    g_actionSet.push_back(std::make_unique<NavigateToFood>());
    g_actionSet.push_back(std::make_unique<NavigateToWood>());
    g_actionSet.push_back(std::make_unique<CollectWood>());
    g_actionSet.push_back(std::make_unique<NavigateToStone>());
    g_actionSet.push_back(std::make_unique<MineStone>());
    g_actionSet.push_back(std::make_unique<NavigateToWater>());
    g_actionSet.push_back(std::make_unique<DrinkWater>());
    g_actionSet.push_back(std::make_unique<NavigateToBuildSite>());
    g_actionSet.push_back(std::make_unique<DeliverWood>());
    g_actionSet.push_back(std::make_unique<DeliverStone>());
    g_actionSet.push_back(std::make_unique<NavigateToCamp>());
    g_actionSet.push_back(std::make_unique<Rest>());
}

static void EnsureAgentSlot(size_t index)
{
    if (g_agentMemories.size() <= index)
    {
        g_agentMemories.resize(index + 1);
        g_agentProfiles.resize(index + 1);
        g_agentBlackboards.resize(index + 1);
    }
}

static AgentProfile MakeProfileForRole(AgentRole role)
{
    switch (role)
    {
        case AgentRole::Blind: return AgentProfile::MakeBlind();
        case AgentRole::Deaf: return AgentProfile::MakeDeaf();
        case AgentRole::WheelchairBound:
        default: return AgentProfile::MakeWheelchairUser();
    }
}

static bool AnyTrue(const MapLayer<bool>& layer)
{
    int total = layer.get_width() * layer.get_height();
    for (int i = 0; i < total; ++i)
    {
        if (layer[i]) return true;
    }
    return false;
}

static MapLayer<int>& ReservationLayerForType(int biomeType)
{
    if (biomeType == BIOME_STONE) return g_stoneReservedBy;
    if (biomeType == BIOME_FOOD) return g_foodReservedBy;
    return g_woodReservedBy;
}

static const MapLayer<bool>& WorldLayerForType(int biomeType)
{
    if (biomeType == BIOME_STONE) return g_worldGrid.stoneLayer;
    if (biomeType == BIOME_FOOD) return g_worldGrid.foodLayer;
    return g_worldGrid.woodLayer;
}

static MapLayer<bool>& MemoryLayerForType(AgentMemory& mem, int biomeType)
{
    if (biomeType == BIOME_STONE) return mem.discoveredStone;
    if (biomeType == BIOME_FOOD) return mem.discoveredFood;
    return mem.discoveredWood;
}

// Finds the nearest tile of `biomeType` that this agent's own memory has
// discovered and that nobody else has claimed, reserves it for `agentHandle`.
static bool FindAndReserveResource(int agentHandle, int biomeType, int agentX, int agentY, int* outX, int* outY)
{
    if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agentMemories.size())) return false;

    AgentMemory& mem = g_agentMemories[agentHandle];
    const MapLayer<bool>& memLayer = MemoryLayerForType(mem, biomeType);
    const MapLayer<bool>& worldLayer = WorldLayerForType(biomeType);
    MapLayer<int>& reserved = ReservationLayerForType(biomeType);

    int width = g_worldGrid.width;
    int height = g_worldGrid.height;
    int bestX = -1, bestY = -1;
    float bestDist = 1e18f;

    for (int y = 0; y < height; ++y)
    {
        for (int x = 0; x < width; ++x)
        {
            if (!memLayer.at(y, x) || !worldLayer.at(y, x)) continue;

            int owner = reserved.at(y, x);
            if (owner != -1 && owner != agentHandle) continue;

            float dx = static_cast<float>(x - agentX);
            float dy = static_cast<float>(y - agentY);
            float dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestX = x;
                bestY = y;
            }
        }
    }

    if (bestX == -1) return false;

    reserved.at(bestY, bestX) = agentHandle;
    *outX = bestX;
    *outY = bestY;
    return true;
}

static bool FindNearestWaterEdge(int agentHandle, int agentX, int agentY, int* outX, int* outY)
{
    if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agentMemories.size())) return false;

    AgentMemory& mem = g_agentMemories[agentHandle];
    int width = g_worldGrid.width;
    int height = g_worldGrid.height;
    int bestX = -1, bestY = -1;
    float bestDist = 1e18f;

    for (int y = 0; y < height; ++y)
    {
        for (int x = 0; x < width; ++x)
        {
            if (!mem.discoveredWaterEdge.at(y, x)) continue;
            float dx = static_cast<float>(x - agentX);
            float dy = static_cast<float>(y - agentY);
            float dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestX = x;
                bestY = y;
            }
        }
    }

    if (bestX == -1) return false;
    *outX = bestX;
    *outY = bestY;
    return true;
}

extern "C" {

    // --------------------------------------------------------
    // GRID / TERRAIN SETUP
    // --------------------------------------------------------

    __declspec(dllexport) int InitializeGrid(int width, int height)
    {
        g_grid.Init(width, height);
        g_worldGrid.initialize(width, height);
        g_woodReservedBy.resize(width, height, -1);
        g_foodReservedBy.resize(width, height, -1);
        g_stoneReservedBy.resize(width, height, -1);
        BuildActionSetIfNeeded();
        return 1;
    }

    // EXPOSED TO UNITY: Feeds the flat procedural data directly into your AStarGrid + WorldGrid
    __declspec(dllexport) void LoadTerrainGrid(SimpleNodeData* gridData, int width, int height)
    {
        if (!gridData) return;

        g_grid.Init(width, height);
        g_worldGrid.initialize(width, height);
        g_woodReservedBy.resize(width, height, -1);
        g_foodReservedBy.resize(width, height, -1);
        g_stoneReservedBy.resize(width, height, -1);
        BuildActionSetIfNeeded();

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int index = (y * width) + x;
                const SimpleNodeData& node = gridData[index];

                // SetBlocked already syncs CellType to 0 (free) or 1 (blocked);
                // rubble tiles (CellType 2, impassable for WheelchairBound
                // only) are tagged afterward via a separate SetCellType call
                // from PrimsTerrainGenerator once resource clusters are placed.
                g_grid.SetBlocked(x, y, !node.isWalkable);
                g_grid.SetCellWeight(x, y, node.movementCost);

                g_worldGrid.wallLayer.at(y, x) = !node.isWalkable;
                g_worldGrid.woodLayer.at(y, x) = (node.biomeType == BIOME_WOOD);
                g_worldGrid.foodLayer.at(y, x) = (node.biomeType == BIOME_FOOD);
                g_worldGrid.stoneLayer.at(y, x) = (node.biomeType == BIOME_STONE);
            }
        }

        // Second pass: a walkable tile counts as a "water edge" if any of its
        // 8 neighbors is Water — lets agents drink without needing a separate
        // carrying mechanic.
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                if (g_worldGrid.wallLayer.at(y, x)) continue;

                bool nearWater = false;
                for (int dy = -1; dy <= 1 && !nearWater; ++dy)
                {
                    for (int dx = -1; dx <= 1 && !nearWater; ++dx)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        if (gridData[(ny * width) + nx].biomeType == BIOME_WATER) nearWater = true;
                    }
                }
                g_worldGrid.waterEdgeLayer.at(y, x) = nearWater;
            }
        }
    }

    __declspec(dllexport) void SetCellBlocked(int x, int y, int blocked)
    {
        g_grid.SetBlocked(x, y, blocked != 0);
    }

    // SetCellType is used to mark environmental obstacles like rubble/stairs (Type 2)
    __declspec(dllexport) void SetCellType(int x, int y, int cellType)
    {
        g_grid.SetCellType(x, y, cellType);
    }

    __declspec(dllexport) void SetCellWeight(int x, int y, float weight)
    {
        g_grid.SetCellWeight(x, y, weight);
    }

    // Lets Unity pre-check a candidate wander tile before spending a full
    // pathfind on it, instead of firing blind and logging a failure every
    // time a random offset lands on water or another blocked cell.
    __declspec(dllexport) int IsWalkable(int x, int y)
    {
        return g_grid.IsBlocked(x, y) ? 0 : 1;
    }

    // Same, but also respects the requesting agent's role — a rubble tile
    // reads as generically walkable but is impassable for WheelchairBound
    // agents specifically (see Agent::FindPath), so a plain IsWalkable check
    // still sends wheelchair users at tiles they can never actually reach.
    __declspec(dllexport) int IsWalkableForAgent(int agentHandle, int x, int y)
    {
        if (g_grid.IsBlocked(x, y)) return 0;

        if (agentHandle >= 0 && agentHandle < static_cast<int>(g_agentProfiles.size()))
        {
            if (g_agentProfiles[agentHandle].disability == DisabilityType::WheelchairUser && g_grid.GetCellType(x, y) == 2)
                return 0;
        }

        return 1;
    }

    __declspec(dllexport) void LoadObstacleData(unsigned char* data, int length)
    {
        g_grid.LoadFromBytes(data, length);
    }

    __declspec(dllexport) void SetBuildSitePosition(int x, int y)
    {
        g_worldGrid.buildSiteX = x;
        g_worldGrid.buildSiteY = y;
    }

    __declspec(dllexport) void SetCampPosition(int x, int y)
    {
        g_worldGrid.campX = x;
        g_worldGrid.campY = y;
    }

    // --------------------------------------------------------
    // PATHFINDING
    // --------------------------------------------------------

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

    // --------------------------------------------------------
    // AGENT LIFECYCLE
    // --------------------------------------------------------

    __declspec(dllexport) int CreateAgent(int role)
    {
        auto agent = std::make_unique<Agent>();
        AgentRole agentRole = static_cast<AgentRole>(role);
        agent->Init(&g_grid, agentRole);

        size_t slot;
        bool recycled = false;
        for (slot = 0; slot < g_agents.size(); ++slot)
        {
            if (g_agents[slot] == nullptr) { recycled = true; break; }
        }

        if (!recycled)
        {
            g_agents.push_back(std::move(agent));
            slot = g_agents.size() - 1;
        }
        else
        {
            g_agents[slot] = std::move(agent);
        }

        EnsureAgentSlot(slot);
        g_agentProfiles[slot] = MakeProfileForRole(agentRole);
        g_agentMemories[slot].initialize(g_worldGrid.width, g_worldGrid.height);
        g_agentBlackboards[slot] = Blackboard();

        return static_cast<int>(slot);
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

    // --------------------------------------------------------
    // SLAM: PERCEPTION + MEMORY SYNC
    // --------------------------------------------------------

    // Called by UnityAgent every time it steps onto a new tile — clears fog
    // of war around the agent (sight radius) and, for agents who can hear,
    // reveals food further away too. This is the SLAM "localize + map" step.
    __declspec(dllexport) void AgentPerceive(int agentHandle, int x, int y)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agentMemories.size())) return;
        UpdatePhysicalSenses(x, y, g_worldGrid, g_agentMemories[agentHandle], g_agentProfiles[agentHandle]);
    }

    // Called when two agents come within communication range of each other —
    // the SLAM "loop closure" step: each agent adopts everything the other
    // has explored so far.
    __declspec(dllexport) void TriggerSLAMSync(int agentHandleA, int agentHandleB)
    {
        if (agentHandleA < 0 || agentHandleA >= static_cast<int>(g_agentMemories.size())) return;
        if (agentHandleB < 0 || agentHandleB >= static_cast<int>(g_agentMemories.size())) return;
        if (agentHandleA == agentHandleB) return;

        AgentMemory& memA = g_agentMemories[agentHandleA];
        AgentMemory& memB = g_agentMemories[agentHandleB];
        memA.Merge(memB);
        memB.Merge(memA);
    }

    // --------------------------------------------------------
    // RESOURCE RESERVATION (GROUP COORDINATION)
    // --------------------------------------------------------

    __declspec(dllexport) int GetAvailableResource(int agentHandle, int biomeType, int agentX, int agentY, int* outX, int* outY)
    {
        return FindAndReserveResource(agentHandle, biomeType, agentX, agentY, outX, outY) ? 1 : 0;
    }

    // Lets Unity visualize SLAM directly: query whether a specific agent's
    // own memory has discovered a given resource tile yet, so a "fog of war
    // lifting" beacon can be shown/hidden per agent instead of SLAM staying
    // an invisible internal data structure.
    __declspec(dllexport) int IsResourceDiscoveredByAgent(int agentHandle, int biomeType, int x, int y)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agentMemories.size())) return 0;
        if (x < 0 || x >= g_worldGrid.width || y < 0 || y >= g_worldGrid.height) return 0;

        AgentMemory& mem = g_agentMemories[agentHandle];
        return MemoryLayerForType(mem, biomeType).at(y, x) ? 1 : 0;
    }

    __declspec(dllexport) void ReleaseResource(int biomeType, int x, int y)
    {
        MapLayer<int>& reserved = ReservationLayerForType(biomeType);
        if (x < 0 || x >= g_worldGrid.width || y < 0 || y >= g_worldGrid.height) return;
        reserved.at(y, x) = -1;
    }

    // Called once a resource tile has actually been harvested/depleted —
    // removes it from ground truth and purges any agent's stale memory of it.
    __declspec(dllexport) void ClearResourceTile(int biomeType, int x, int y)
    {
        if (x < 0 || x >= g_worldGrid.width || y < 0 || y >= g_worldGrid.height) return;

        if (biomeType == BIOME_STONE) g_worldGrid.stoneLayer.at(y, x) = false;
        else if (biomeType == BIOME_FOOD) g_worldGrid.foodLayer.at(y, x) = false;
        else g_worldGrid.woodLayer.at(y, x) = false;

        ReservationLayerForType(biomeType).at(y, x) = -1;

        for (auto& mem : g_agentMemories)
        {
            MemoryLayerForType(mem, biomeType).at(y, x) = false;
        }
    }

    // --------------------------------------------------------
    // GOAP: THE ACTUAL DECISION-MAKING
    // --------------------------------------------------------

    __declspec(dllexport) void SyncAgentBlackboard(int agentHandle, float hunger, float thirst, float fatigue,
        int woodCarried, int maxWood, int stoneCarried, int maxStone)
    {
        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agentBlackboards.size())) return;

        Blackboard& bb = g_agentBlackboards[agentHandle];
        bb.hunger = hunger;
        bb.thirst = thirst;
        bb.fatigue = fatigue;
        bb.carriedWood = woodCarried;
        bb.maxWoodCapacity = maxWood;
        bb.carriedStone = stoneCarried;
        bb.maxStoneCapacity = maxStone;
    }

    // The core GOAP entry point. Builds the agent's current WorldState from
    // its (synced) Blackboard + ground truth at its tile + its own SLAM
    // memory, tries goals in priority order, and runs the A* planner over
    // WorldState space for each until one succeeds. Returns the action code
    // for the first step of the winning plan; for Navigate* actions also
    // resolves (and reserves) a concrete target tile.
    __declspec(dllexport) int PlanNextAction(int agentHandle, int agentX, int agentY, int* outTargetX, int* outTargetY)
    {
        *outTargetX = agentX;
        *outTargetY = agentY;

        if (agentHandle < 0 || agentHandle >= static_cast<int>(g_agents.size()) || !g_agents[agentHandle])
            return ACT_ExploreUnknown;

        AgentMemory& mem = g_agentMemories[agentHandle];
        AgentProfile& profile = g_agentProfiles[agentHandle];
        Blackboard& bb = g_agentBlackboards[agentHandle];

        WorldState current;
        bb.PopulateFacts(current);

        bool inBounds = agentX >= 0 && agentX < g_worldGrid.width && agentY >= 0 && agentY < g_worldGrid.height;
        current.Set("at_food", inBounds && g_worldGrid.foodLayer.at(agentY, agentX));
        current.Set("at_wood", inBounds && g_worldGrid.woodLayer.at(agentY, agentX));
        current.Set("at_stone", inBounds && g_worldGrid.stoneLayer.at(agentY, agentX));
        current.Set("at_water", inBounds && g_worldGrid.waterEdgeLayer.at(agentY, agentX));
        current.Set("at_build_site", agentX == g_worldGrid.buildSiteX && agentY == g_worldGrid.buildSiteY);
        current.Set("at_camp", agentX == g_worldGrid.campX && agentY == g_worldGrid.campY);

        current.Set("food_location_known", AnyTrue(mem.discoveredFood));
        current.Set("wood_location_known", AnyTrue(mem.discoveredWood));
        current.Set("stone_location_known", AnyTrue(mem.discoveredStone));
        current.Set("water_location_known", AnyTrue(mem.discoveredWaterEdge));

        // Goal priority: survive first, then contribute to the group build.
        std::vector<WorldState> goals;
        auto addGoal = [&](const char* fact, bool value) {
            WorldState g;
            g.Set(fact, value);
            goals.push_back(g);
        };

        if (current.Get("thirst_critical")) addGoal("thirst_critical", false);
        if (current.Get("hunger_critical")) addGoal("hunger_critical", false);
        if (current.Get("fatigue_high"))    addGoal("fatigue_high", false);
        if (current.Get("wood_full"))       addGoal("has_wood", false);
        if (current.Get("stone_full"))      addGoal("has_stone", false);
        // Default work: gather whichever resource this agent isn't already carrying.
        addGoal("has_wood", true);
        addGoal("has_stone", true);
        addGoal("map_updated", true); // last resort: explore

        GOAPPlanner planner;
        for (const WorldState& goal : goals)
        {
            PlanResult result = planner.Plan(current, goal, g_actionSet, profile);
            if (!result.success || result.actions.empty()) continue;

            int code = result.actions[0]->GetCode();

            // Resolve (and reserve) a concrete destination for travel actions.
            // If nothing is actually reservable despite the planner thinking
            // the location was "known", fall through to the next goal instead
            // of sending the agent to a stale/contested tile.
            bool resolved = true;
            switch (code)
            {
                case ACT_NavigateToWood: resolved = FindAndReserveResource(agentHandle, BIOME_WOOD, agentX, agentY, outTargetX, outTargetY); break;
                case ACT_NavigateToFood: resolved = FindAndReserveResource(agentHandle, BIOME_FOOD, agentX, agentY, outTargetX, outTargetY); break;
                case ACT_NavigateToStone: resolved = FindAndReserveResource(agentHandle, BIOME_STONE, agentX, agentY, outTargetX, outTargetY); break;
                case ACT_NavigateToWater: resolved = FindNearestWaterEdge(agentHandle, agentX, agentY, outTargetX, outTargetY); break;
                case ACT_NavigateToBuildSite: *outTargetX = g_worldGrid.buildSiteX; *outTargetY = g_worldGrid.buildSiteY; resolved = (g_worldGrid.buildSiteX >= 0); break;
                case ACT_NavigateToCamp: *outTargetX = g_worldGrid.campX; *outTargetY = g_worldGrid.campY; resolved = (g_worldGrid.campX >= 0); break;
                default: *outTargetX = agentX; *outTargetY = agentY; break; // stationary action, agent is already there
            }

            if (resolved) return code;
        }

        return ACT_ExploreUnknown;
    }

    // --------------------------------------------------------
    // PRIM'S ALGORITHM: PATH NETWORK GENERATION
    // --------------------------------------------------------

    // Runs a genuine Prim's-algorithm MST over the given points of interest
    // (camp, build site, resource cluster centroids) and returns the tree's
    // edges as index pairs into the input arrays. Unity rasterizes each edge
    // into a line of low-weight path tiles.
    __declspec(dllexport) int GeneratePathNetwork(int* xs, int* ys, int count, int* outFromIdx, int* outToIdx, int maxEdges)
    {
        if (!xs || !ys || count <= 0) return 0;

        std::vector<int> xVec(xs, xs + count);
        std::vector<int> yVec(ys, ys + count);
        std::vector<PathEdge> edges = ComputePrimsMST(xVec, yVec);

        int written = 0;
        for (const PathEdge& edge : edges)
        {
            if (written >= maxEdges) break;
            outFromIdx[written] = edge.fromIndex;
            outToIdx[written] = edge.toIndex;
            ++written;
        }
        return written;
    }
}
