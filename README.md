# Survival Simulation — CSD3183 Final Project

A Unity survival simulation where a group of physically-impaired agents
(wheelchair-bound, blind, deaf) must cooperatively gather wood, stone, food,
and water, and build a house — using **GOAP**, **SLAM**, **weighted A\***,
and **Prim's algorithm**, all implemented natively in **C++** and linked into
Unity through a custom native plugin.

## Contents

- [Quick start](#quick-start)
- [Architecture overview](#architecture-overview)
- [Algorithms](#algorithms)
  - [GOAP (Goal-Oriented Action Planning)](#goap-goal-oriented-action-planning)
  - [SLAM (simplified fog-of-war memory)](#slam-simplified-fog-of-war-memory)
  - [Weighted A\* pathfinding](#weighted-a-pathfinding)
  - [Prim's algorithm](#prims-algorithm)
  - [Supporting algorithms](#supporting-algorithms)
- [Impairments](#impairments)
- [C++ ↔ C# interop](#c-c-interop)
- [Project structure](#project-structure)
- [Simulation UI & controls](#simulation-ui--controls)
- [Known limitations](#known-limitations)

## Quick start

1. Open the project in Unity and load `Assets/Scenes/TerrainTestScene.unity`.
2. Press Play. A terrain generates, agents spawn near the camp, and the
   simulation settings panel appears on-screen.
3. If you've edited any file under `PathfinderCore/`, close Unity first, then
   rebuild the native plugin:
   ```
   cd PathfinderCore
   MSBuild.exe PathfinderCore.sln /p:Configuration=Release /p:Platform=x64
   ```
   This writes directly to `Assets/Plugins/PathfinderCore.dll`, which Unity
   loads on the next Play — the build fails with `LNK1104` if Unity still has
   the DLL open, so it must be closed first.

## Architecture overview

```
┌─────────────────────────────┐        P/Invoke        ┌───────────────────────────────┐
│  Unity (C#)                 │ ──────────────────────▶ │  PathfinderCore.dll (C++)     │
│  Assets/Scripts/            │                         │  PathfinderCore/               │
│                             │ ◀────────────────────── │                                │
│  - Terrain generation       │   handles / bools /     │  - AStarGrid + Agent (weighted │
│  - Agent visuals & movement │   int codes only         │    A*, per-role constraints)   │
│  - GOAP action execution    │   (no shared objects,    │  - GOAP planner + actions      │
│  - UI                       │   no complex structs     │  - SLAM (AgentMemory,          │
│                             │   except one POD array)  │    Perception, WorldGrid)      │
│                             │                          │  - Prim's MST (PathNetwork)    │
└─────────────────────────────┘                         └───────────────────────────────┘
```

All decision-making state — the GOAP planner, every agent's SLAM memory, the
pathfinding grid, resource reservations — lives in C++ static globals inside
`PathfinderCore.dll`. Unity only ever holds an integer **agent handle** per
agent and calls into the DLL to ask "what should this agent do next" or "find
me a path from A to B." No gameplay logic that's supposed to be in C++ (GOAP,
SLAM, weighted A*) is duplicated in C#.

## Algorithms

### GOAP (Goal-Oriented Action Planning)

**Where:** `PathfinderCore/goap/` (`WorldState.h`, `Action.h`,
`AgentActions.h`, `GOAPPlanner.h/.cpp`), driven from `PluginMain.cpp`'s
`PlanNextAction`.

GOAP is a real forward-search planner, not a hardcoded if/else chain. Every
tick, for a given agent:

1. Unity calls `SyncAgentBlackboard` with that agent's live hunger, thirst,
   fatigue, and inventory. The native `Blackboard::PopulateFacts` converts
   these into boolean **facts** (`WorldState`) using fixed thresholds:
   - `thirst_critical = thirst > 75`
   - `hunger_critical = hunger > 80`
   - `fatigue_high = fatigue > 85`
   - plus facts derived from the agent's own SLAM memory:
     `wood_location_known`, `food_location_known`, `stone_location_known`,
     `water_location_known` (true if that resource layer has any discovered
     tile in the agent's `AgentMemory`), and `at_wood`/`at_food`/`at_stone`/
     `at_water`/`at_camp`/`at_build_site` (true if the agent's current tile
     matches).
2. `PlanNextAction` tries a fixed list of **goals**, in order, and uses the
   **first one that a valid plan can actually be found for**:

   | # | Goal | Only added if… |
   |---|------|-----------------|
   | 1 | `thirst_critical → false` | thirst is currently critical |
   | 2 | `hunger_critical → false` | hunger is currently critical |
   | 3 | `fatigue_high → false` | fatigue is currently high |
   | 4 | `has_wood → false` (deliver) | wood inventory is full |
   | 5 | `has_stone → false` (deliver) | stone inventory is full |
   | 6 | `has_wood → true` (gather) | *always* |
   | 7 | `has_stone → true` (gather) | *always* |
   | 8 | `map_updated → true` (explore) | *always* (last resort, always satisfiable) |

   Goals 1–5 are conditional — thirst/hunger/fatigue survival needs and
   full-inventory deliveries only enter the list when they're actually true
   this tick. Goals 6–8 are unconditional fallbacks, tried in that fixed
   order: an agent with nothing urgent to do always tries to gather wood
   first, and only falls back to stone-gathering if no wood location is
   known (not "whichever resource is neediest").
3. For the winning goal, `GOAPPlanner::Plan` does an A\* search over
   `WorldState` fact-space (nodes are fact combinations, edges are actions
   whose preconditions match, edge cost is `Action::GetCost`) and returns an
   ordered action chain. `PlanNextAction` returns the **first action's**
   code; Unity executes it (moves there if it's a `Navigate*` action, then
   runs the stationary effect next tick) and re-plans from scratch.
4. Costs — not just preconditions — are where impairments show up. Every
   `Navigate*` action's `GetCost` is scaled by `AgentProfile.movementCostMultiplier`
   (role-dependent), `ExploreUnknown` additionally multiplies in a `2.5×`
   penalty for Blind agents, and `MineStone` adds a `1.5×` penalty for
   WheelchairBound agents. `DetectFoodBySound` is outright unavailable
   (`CheckProceduralPrecondition` returns false) for Deaf agents
   (`hearingRange == 0`), rather than just being expensive — a huge cost
   isn't enough, since the planner would still technically "solve" through it
   if it were the only action satisfying a goal.

Action codes (`ActionCode` in `AgentActions.h`, mirrored in
`Assets/Scripts/Agent/GOAP/AgentAction.cs`):

```
0 ExploreUnknown      5 DetectFoodBySound   10 NavigateToBuildSite
1 NavigateToWood       6 NavigateToStone     11 DeliverWood
2 CollectWood          7 MineStone           12 DeliverStone
3 NavigateToFood       8 NavigateToWater     13 NavigateToCamp
4 Eat                  9 DrinkWater          14 Rest
```

### SLAM (simplified fog-of-war memory)

**Where:** `PathfinderCore/spatial/` (`AgentMemory.h`, `Perception.h`,
`WorldGrid.h`, `MapLayer.h`).

This isn't full probabilistic SLAM (position is always known exactly — no
localization uncertainty) — the "simultaneous localization and mapping" part
that's implemented is the **mapping**: each agent keeps its own private,
partial view of the map (`AgentMemory`), separate from ground truth
(`WorldGrid`), and that view is built up and shared as agents move and meet.

- **Sight** (`profile.sightRadius`, tiny for Blind agents) reveals a small
  radius around the agent **instantly and completely** every time
  `AgentPerceive` runs — walls, wood, food, stone, and water-edge tiles all
  become known at once.
- **Hearing** (`profile.hearingRange`, `0` for Deaf agents — the actual
  enforced "hearing impaired" rule) does two things, much farther than sight:
  - Food is heard immediately and precisely (rustling/animals are
    distinctive), regardless of general map knowledge.
  - Everything else about a tile in hearing range is only picked up
    **slowly** — a 6% chance per `AgentPerceive` call to reveal that tile,
    rather than sight's instant guaranteed reveal. Since `AgentPerceive`
    fires once per grid tile the agent moves onto, this reads as a gradual
    map reveal built up over several nearby passes, distinct from sight.
  This is what makes Blind agents (tiny sight, wide hearing) explore a large
  area slowly, while Deaf agents (no hearing at all) are stuck with sight's
  small-but-instant radius.
- **Sync** (`TriggerSLAMSync`, called from `SlamCoordinator.cs` for any two
  agents within a communication radius): `AgentMemory::Merge()` does a
  per-tile **bitwise-OR-assignment** (`|=`) union of every discovered layer
  from the other agent into this one — knowledge spreads through the group
  without a shared omniscient map.

`FogOfWarOverlay.cs` visualizes this directly: select an agent in the
overlay UI and a dark quad covers every tile that agent hasn't personally
discovered yet. `SlamDiscoveryBeacons.cs` similarly lights up a beacon over
each wood/food/stone tile only once the selected agent's own memory has
found it.

### Weighted A* pathfinding

**Where:** `PathfinderCore/AStarGrid.h/.cpp` (shared grid + per-cell weight)
and `PathfinderCore/Agent.cpp` (per-role constraints layered on top).

Standard 8-directional A\* (diagonal cost `√2`, corner-cutting prevented),
except every tile carries a **movement-weight multiplier**
(`AStarGrid::_cellWeight`, set from Unity's per-tile `movementCost` via
`SetCellWeight`) that's multiplied into the move cost — a path tile might be
`0.5×` (cheap), rough terrain `2.0×` (expensive). Without this the algorithm
would just be plain A* with no actual "weighted" behavior — this multiply
was previously a dropped/no-op field, fixed as part of this project.

Two more cost layers stack on top of the terrain weight, both role-aware:

- **Sound cues** (`Agent::SoundPenaltyAt`): a `SoundCue{x, y, radius,
  costPenalty}` adds extra cost to nearby tiles for any hearing agent — Deaf
  agents (`_role == Deaf`) ignore sound cues entirely, unaffected.
- **Role-specific cell costs** (`Agent::RoleCellCostMultiplier` /
  `isBlocked`): rubble tiles (native `CellType 2`) are fully impassable for
  WheelchairBound agents only; bridge/water-crossing tiles (`CellType 3`,
  the only walkable water tiles — everything else is fully blocked for
  everyone) aren't blocked for WheelchairBound (the map must stay
  traversable for every role) but cost `4×` more to cross than open ground,
  so a wheelchair user visibly detours around a bridge when an alternative
  route exists.

### Prim's algorithm

**Where:** `PathfinderCore/PathNetwork.cpp`'s `ComputePrimsMST` — a genuine
Prim's-algorithm minimum spanning tree (frontier set, repeatedly pick the
cheapest edge into an unvisited node), not a DFS maze-carver and not Perlin
noise. Exported as `GeneratePathNetwork` and used twice, independently, from
Unity:

1. **Water network** (`PrimsTerrainGenerator.GenerateWaterNetwork`): scatters
   random seed points, connects them with a Prim's MST, then meanders a
   river along each MST edge (Perlin-noise-based meander, see below) —
   randomized layout every generation, but always a single connected river
   network instead of scattered disconnected lakes.
2. **Path network** (`PrimsPathNetworkBuilder.cs`): connects camp, build
   site, and resource-cluster centroids with a Prim's MST, then rasterizes
   each MST edge into low-weight path tiles — feeding directly into the
   weighted-A* system above.

### Supporting algorithms

- **BFS/flood-fill connectivity guarantee**
  (`PrimsTerrainGenerator.EnsureFullMapConnectivity`): after the randomized
  water network and ponds are carved, a flood-fill finds every disconnected
  walkable "island" and carves a forced bridge to the main landmass — a hard
  backstop so a bad random roll never produces an unreachable pocket of the
  map.
- **Bresenham line rasterization**: used both for river-segment carving and
  for turning a Prim's MST edge into a walkable line of path tiles.
- **Perlin noise**: used only for the river's meander (how much a river
  segment drifts from the straight line between its two MST endpoints) —
  distinct from Prim's algorithm, which decides the network's macro layout,
  not its micro wiggle.

## Impairments

Every agent gets the **same** action set and the **same** goal priorities —
nobody is locked out of a task entirely. Only costs, senses, and terrain
constraints differ by role (`AgentProfile` factories in `WorldState.h`),
which is what makes an impairment visibly *affect* behavior instead of just
being a re-skinned label:

| | WheelchairBound | Blind | Deaf |
|---|---|---|---|
| Sight radius | 5 | 1 (tiny) | 7 (best — compensates) |
| Hearing range | 5 | 8 (wide) | 0 (none) |
| Movement | `0.6×` speed in Unity; rubble tiles fully impassable; bridges cost `4×` more | Normal speed; `2.5×` explore-action cost, otherwise normal | `0.9×` GOAP move-action cost (slightly cheaper — nothing to be cautious about) |
| Sound | Normal — pays extra A* cost near active sound cues | Normal | Immune — ignores sound cues and can't use `DetectFoodBySound` at all |
| Visual tell | Blue tint, flattened "wheel disc" base | Red tint, dark blindfold band | Orange tint, two ear-cover spheres |

Every agent also gets a **sight ring** (`ImpairmentVisuals.AddSightRing`)
sized to its own sight radius, and status bars for hunger/thirst/fatigue —
select any agent in the on-screen overlay to see its individual state, fog
of war, and current GOAP action live.

## C++ ↔ C# interop

- **Marshaling**: `Assets/Scripts/NativeBridge.cs` declares every
  `[DllImport("PathfinderCore", CallingConvention = CallingConvention.Cdecl)]`
  extern function, matching an `extern "C" __declspec(dllexport)` export in
  `PathfinderCore/PluginMain.cpp`. Only primitives (`int`, `float`, `bool` as
  `int`), primitive arrays, and one `[StructLayout(LayoutKind.Sequential,
  Pack = 1)]` POD struct (`SimpleNodeData`, matching a C++ `#pragma
  pack(push, 1)` struct exactly field-for-field) ever cross the boundary —
  no shared objects, no virtual calls, no STL types.
- **State ownership**: all gameplay state (the grid, every agent's
  `AgentMemory`/`AgentProfile`/`Blackboard`, resource reservations, sound
  cues) lives in C++ static globals (`g_grid`, `g_agentMemories`,
  `g_agentProfiles`, …), indexed by an integer **agent handle**. Unity's
  `UnityAgent`/`AgentGOAP` hold only that handle — never a pointer, never a
  reference to native memory.
- **Call shape**: Unity pushes state in (`SyncAgentBlackboard`,
  `AgentPerceive`, `LoadTerrainGrid`), and pulls decisions out
  (`PlanNextAction`, `FindAgentPath`, `GetExploredTiles`) — every native call
  is a plain request/response, no callbacks in either direction.
- **Build**: `PathfinderCore.sln` builds the native plugin directly into
  `Assets/Plugins/PathfinderCore.dll` (its `OutDir` points there). Unity must
  be closed before rebuilding, or the linker fails with `LNK1104` (file
  locked by the Editor).

## Project structure

```
PathfinderCore/                  Native C++ plugin (GOAP, SLAM, weighted A*, Prim's)
├── goap/                        WorldState, GOAPPlanner, AgentActions
├── spatial/                     AgentMemory (SLAM), Perception, WorldGrid, MapLayer
├── AStarGrid.*, Agent.*         Weighted A* + per-role pathfinding constraints
├── PathNetwork.*                Prim's MST (ComputePrimsMST)
└── PluginMain.cpp               All extern "C" exports Unity calls into

Assets/Scripts/
├── NativeBridge.cs              Every [DllImport] declaration
├── Agent/                       Movement, GOAP execution, impairment visuals, SLAM
│   └── GOAP/AgentAction.cs      C# mirror of the native ActionCode enum
├── Terrain/                     Procedural generation, Prim's water/path networks
├── UIEvents/                    Simulation settings panel wiring
└── Utility/                     Camera controller, misc helpers
```

## Simulation UI & controls

- **Camera** (`FreeFlyCamera`): WASD to move, right-click-drag to look, E/Q
  for up/down. Selecting an agent in the overlay can lock the camera to
  follow it; the "Map Alignment" toggle snaps to a fixed top-down view
  instead.
- **Overlay panel** (top-left): live list of every agent with hunger/thirst/
  fatigue bars — click one to select it (locks camera, shows its fog of war,
  SLAM discovery beacons, and current GOAP action).
- **Settings panel**: random-map regeneration, restart, simulation speed,
  pause, debug-drawing toggle (sight rings/beacons), agent creation/kill/
  type-change. (A "Load Map" and "Map Editor" button existed earlier but
  were removed along with their now-dead wiring.)

## Known limitations

- `AgentProfile.fatigue`/`cognitiveLoad` fields exist but are never written
  to or read from — fatigue affects GOAP goal priority (via the
  `fatigue_high` fact) but has no effect on movement/action cost and no death
  consequence, unlike hunger and thirst.
- Sound cues (`AddSoundCue`) are a fully-implemented, real A* cost mechanic,
  but nothing in the live simulation currently calls it — only a standalone
  manual test script does, so the hearing-avoidance behavior it enables is
  built but currently dormant during normal play.
