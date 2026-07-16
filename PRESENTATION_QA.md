# Presentation Prep — Anticipated Questions

Organized by topic. Read `README.md` first for the full picture — this file
is the "defend it out loud" version: short, direct answers you should be
able to say without looking, plus the follow-ups that tend to come after.

---

## GOAP

**Q: What is GOAP, in one sentence?**
A planning technique where an agent picks actions based on their
preconditions/effects/cost rather than following a scripted sequence — you
give it a goal and a set of actions, and it searches for the cheapest chain
of actions that reaches that goal from the current state.

**Q: How does your planner actually search?**
It's a real A* search, but over *world-state space* instead of grid space:
each search node is a hypothetical world state (a set of boolean facts),
each edge is an applicable action, edge cost is that action's cost, and the
heuristic is the number of goal facts not yet satisfied. `GOAPPlanner::Plan`
in `PathfinderCore/goap/GOAPPlanner.cpp`.

**Q: How does it choose *which* goal to pursue?**
Fixed priority order, checked every decision tick: thirst → hunger →
fatigue → deliver whatever resource is full → gather whichever resource is
neediest → explore as a fallback. It tries planning for each in order and
uses the first one that actually finds a valid plan.

**Q: Why would a goal *not* find a plan?**
Mainly: the precondition chain is unreachable given what that agent
currently knows. E.g. the "deliver wood" goal needs `at_build_site`, which
needs `NavigateToBuildSite` — always reachable — but "gather wood" needs
`wood_location_known`, which is only true if that agent's own SLAM memory
has actually discovered a wood tile. If not, that goal search fails and it
falls through to the next priority (or to `ExploreUnknown`, which has no
preconditions and always succeeds).

**Q: Why C++ instead of C#? Wasn't that harder?**
It's a project requirement — GOAP/SLAM/A* had to be implemented in C++.
Practically it also means the actual decision-making logic is
engine-independent and testable outside Unity. The trade-off is the P/Invoke
boundary: every native call has marshaling overhead, so per-agent decisions
are made on a cooldown timer (not every frame) rather than continuously.

**Q: Does every agent use the same actions?**
Yes — deliberately. Every role shares the exact same action set and goal
priorities. The *only* thing that differs per role is action cost
(`Action::GetCost`, reads `AgentProfile`) and a couple of hard preconditions
(a deaf agent's `DetectFoodBySound` is flatly inapplicable, not just
expensive — see `CheckProceduralPrecondition`). This was a deliberate design
choice over rigid "blind agents can only forage" role-locking: it's a
better demonstration of the *same* planner adapting to different
constraints, and no agent is ever excluded from contributing.

**Q: What happens if no plan is found at all?**
`PlanNextAction` falls all the way through to `ACT_ExploreUnknown` as an
unconditional last resort — it has no preconditions so it's always
reachable, and exploring passively grows that agent's SLAM memory, which is
usually what was missing.

---

## SLAM

**Q: SLAM usually means localization *and* mapping. What's the
"localization" part here?**
Simplified/trivial by design — grid position is always known exactly
(there's no positional uncertainty to resolve). The interesting half, and
the one actually implemented, is the **mapping**: each agent independently
builds its own partial map of the world as it moves, which is the core idea
SLAM is built on.

**Q: What exactly does an agent "know"?**
Its own `AgentMemory` (`PathfinderCore/spatial/AgentMemory.h`): which tiles
it's explored (fog-of-war), and for each explored tile, whether it's a
wall/wood/food/stone/water-edge. Nothing is shared automatically.

**Q: How does an agent discover new tiles?**
`AgentPerceive(handle, x, y)` — called from `UnityAgent` every time it steps
onto a new grid tile — runs `UpdatePhysicalSenses`, which clears fog-of-war
within that agent's **sight radius** (varies by role), plus a separate,
longer-range **hearing** sweep that reveals food specifically for any agent
with `hearingRange > 0`.

**Q: How does knowledge spread between agents?**
Proximity-based merge. `SlamCoordinator.cs` checks all agent pairs every
half-second; if two are within a communication radius, `TriggerSLAMSync`
calls `AgentMemory::Merge` both directions — each adopts everything the
other has explored.

**Q: Why does this matter for GOAP, concretely?**
Every fact GOAP reasons about (`wood_location_known`, etc.) is read from
that specific agent's memory, not a shared omniscient map. Two agents
standing next to the same undiscovered tree can behave completely
differently. This is also *why* the blind role makes sense mechanically —
tiny sight radius means it depends much more heavily on either hearing or a
teammate's sync to ever find anything.

**Q: How is the fog-of-war actually rendered?**
One quad spanning the whole map with a dynamically-updated `Texture2D` — one
pixel per grid tile, alpha driven by that agent's `exploredTiles` bitmap.
Deliberately *not* one GameObject per tile (2,500 of them on a 50×50 map) —
`NativeBridge.GetExploredTiles` pulls the whole bitmap in one native call
and the texture updates in one `SetPixels32`/`Apply()`.

**Q: Is this a "real" SLAM implementation (particle filters, occupancy
grids, loop closure, etc.)?**
No, and it's not claiming to be — it's a simplified, grid-based, ground-truth
version that captures the core idea (independent partial mapping +
knowledge merging) without the probabilistic localization machinery real
SLAM needs, which isn't relevant when position is always known exactly.

---

## Weighted A*

**Q: What makes it "weighted"?**
Every cell has a movement-weight multiplier (`AStarGrid::SetCellWeight`)
that multiplies into the cost of entering that tile, on top of the base
straight/diagonal cost. Default terrain is `1.0`; Prim's-algorithm path
tiles are `0.4`. Actual bug fixed this session: this field existed and was
exported from Unity but silently dropped on the native side — the search
was uniform-cost until that was wired through.

**Q: How does an agent's role affect pathfinding specifically?**
`Agent::FindPath` (the per-agent variant, `PathfinderCore/Agent.cpp`) adds
role-specific rules on top of the shared grid: wheelchair-bound agents treat
`CellType == 2` (rubble, tagged near stone deposits) as outright impassable;
hearing agents treat tiles near an active sound cue as more costly to path
through; deaf agents ignore sound cues entirely.

**Q: 4-directional or 8?**
8-directional, with diagonal cost `√2` vs `1` for straight moves, and
corner-cutting blocked (both flanking orthogonal cells must be open before a
diagonal move between them is allowed) — see `AStarGrid::FindPath`.

**Q: Why does the wheelchair constraint use a "cell type" instead of just
blocking the tile outright?**
Because it's role-specific, not universal — the same tile is fully walkable
for the other two roles. A single "blocked" bit can't represent that; a
`CellType` value lets different roles interpret the same tile differently.

---

## Prim's Algorithm

**Q: Where, exactly, is Prim's algorithm used?**
Twice, both in `PathfinderCore/PathNetwork.cpp` (`ComputePrimsMST`), called
from two different C# generation passes:
1. **Water network** — scattered random seed points connected into an MST,
   each edge carved into a meandering river/lake segment.
2. **Path network** — camp + build site + resource-cluster centroids
   connected into an MST, each edge rasterized into low-weight path tiles.

**Q: Is it actually Prim's, or something dressed up as it?**
Genuinely Prim's: maintain a frontier of "cheapest known edge into the tree
so far" for every unvisited node, repeatedly take the globally cheapest one,
relax its neighbors, repeat until every node is in the tree. That's the
textbook definition — not a DFS maze carver (which is what
`LevelEditor/ProceduralGeneration.cs` uses for its maze) and not Perlin
noise (which only perturbs the meander *within* an already-decided edge).

**Q: Why Prim's over Kruskal's?**
Both produce a valid MST; Prim's was specified as a requirement. Practically
Prim's suits this use case well since it grows from a single connected tree
outward, which maps naturally onto "connect these key locations without
needing a separate union-find structure."

**Q: What guarantees the whole map is actually traversable, not just the
MST-connected points?**
Two layers: the MST itself guarantees the *key points* (camp, build site,
resource clusters) are connected. Separately, `EnsureFullMapConnectivity`
(a flood-fill/BFS pass, not Prim's) checks that *every* walkable tile on the
map is reachable from every other, and bridges any leftover disconnected
pocket. Both matter — Prim's decides the deliberate route; the BFS pass is
the hard safety net.

---

## Terrain Generation (supporting algorithms)

**Q: What's the BFS/flood-fill doing exactly?**
Standard connected-components flood fill over every walkable tile (4-
directional). If more than one component exists, it repeatedly finds the
closest pair of tiles between the largest component and each smaller one,
and carves a straight bridge (Bresenham line) between them until only one
component remains.

**Q: What's Bresenham's algorithm used for here?**
Rasterizing a straight line between two grid points one tile at a time
without floating-point coordinates — used for the connectivity-repair
bridges, the periodic forced-bridge crossings along a river segment, and
turning each Prim's path-network edge into an actual line of path tiles.

**Q: Where does Perlin noise come in, and is it doing the same job as
Prim's?**
No — different jobs entirely. Prim's decides the *macro layout* (which
points connect to which). Perlin noise is used only to make each individual
river segment's centerline drift smoothly instead of being a straight
ditch between its two endpoints (`CarveMeanderingRiverSegment`).

---

## Impairments & Group Behavior

**Q: Summarize the three roles and what's actually different.**
| Role | Sight | Hearing | Speed | Unique constraint |
|---|---|---|---|---|
| Wheelchair | 5 | normal | 1.6× slower, mining 1.5× costlier | Can't cross rubble near stone deposits |
| Blind | 1 (tiny) | 8 (far) | 2.5× slower exploring | Can't see resources from a distance at all |
| Deaf | 7 (wide) | none | 0.9× (fastest) | Can't hear food or sound cues at all |

**Q: How do agents avoid duplicating work (two agents chasing the same
tree)?**
Native resource reservation (`FindAndReserveResource` in `PluginMain.cpp`):
when an agent's plan calls for navigating to a resource, it searches *its
own* discovered tiles for the nearest one not already claimed by another
agent's handle, and reserves it. A dead/failed agent releases its claim so
it doesn't get permanently locked away from the rest of the group.

**Q: What happens when an agent dies?**
Hunger or thirst hitting 100 (or a forced kill from the settings UI) marks
it dead; it stops acting and moving but stays visible with a red-X "crossed
out" marker instead of disappearing, and any resource tile it had reserved
is released back to the group.

---

## Architecture / Engineering Decisions

**Q: Why split C++ (planning/pathing/memory) from C# (execution/rendering)
at all, instead of one or the other?**
The project requires the algorithmic core in C++. C# owns execution
(movement, visuals, UI) because that's what Unity is for, and iterating on
presentation doesn't need a native rebuild. The boundary is deliberately
thin: native functions are queries/commands (`PlanNextAction`,
`AgentPerceive`, `FindAgentPath`), and Unity is the only thing that mutates
gameplay state (inventory, stats) — native never reaches back into Unity.

**Q: How do C# and C++ actually talk to each other?**
P/Invoke (`DllImport`) — `NativeBridge.cs` declares the full contract, one
`[DllImport]` per native `extern "C"` export in `PluginMain.cpp`. Struct
layouts that cross the boundary (`SimpleNodeData`) are explicitly packed
(`[StructLayout(LayoutKind.Sequential, Pack = 1)]`) to match the C++ side
byte-for-byte.

**Q: What was the single trickiest bug this session?**
Two contenders worth knowing cold:
1. The weighted-A* bug above (field existed, silently dropped natively).
2. A merge from a teammate's branch that shifted only *visual* tile
   positions by half the map size while pathfinding/agents/camera all kept
   the original coordinates — looked like "the camera is wrong" but was
   actually the terrain rendering somewhere the game logic didn't agree it
   was. Fixed by reverting that specific hunk rather than chasing the
   symptom on the camera side.

**Q: What would you change with more time?**
Reasonable, honest answers: proper occupancy-grid SLAM with uncertainty
instead of ground-truth fog-of-war; multithreading the native planner so
many agents don't all decide on the same frame; a real save/load UI instead
of "load whatever's newest in the folder"; agent role reassignment without a
full destroy-and-respawn.

**Q: Is any of this thread-safe / does it scale?**
No threading — everything native runs on Unity's main thread, called
synchronously. Fine at the current scale (handful of agents, 50×50 map);
a much larger simulation would need to either batch decisions across
frames (already partially true — decisions are cooldown-gated, not
per-frame) or move planning off the main thread.
