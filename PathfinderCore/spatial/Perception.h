// spatial/Perception.h
//
// Turns raw WorldGrid ground-truth into one agent's subjective AgentMemory.
// Sight clears fog-of-war instantly in a small radius (tiny for Blind
// agents). Hearing does two things for any agent with a positive
// hearingRange (Deaf agents get neither): it hears food precisely and
// immediately (distinctive rustling/animal sounds), and it slowly builds up
// general map knowledge from ambient sound — a small per-call chance to
// reveal each tile in hearing range, rather than sight's instant full
// reveal. That's what makes SLAM knowledge genuinely different per role
// instead of just a smaller circle: Blind agents (tiny sight, wide hearing)
// end up exploring a large area gradually; Deaf agents (no hearing at all)
// rely entirely on sight's instant-but-small radius.
#pragma once
#include "spatial/WorldGrid.h"
#include "spatial/AgentMemory.h"
#include "goap/WorldState.h"
#include <cstdlib>

namespace
{
    // Chance any single unexplored tile within hearing range gets revealed
    // on one AgentPerceive call. AgentPerceive fires once per grid tile the
    // agent moves onto, so this accumulates into a gradual reveal over
    // several moves rather than an instant one, unlike sight.
    constexpr float kHearingDiscoveryChance = 0.06f;

    inline bool RandomChance(float probability)
    {
        return (static_cast<float>(std::rand()) / static_cast<float>(RAND_MAX)) < probability;
    }
}

// Instantly reveals every tile within `radius` of (cx, cy) into `memory` —
// shared by the sight sweep below and by PluginMain.cpp's
// RevealAreaForAgent (a food tile's periodic sound pulse, broadcast from
// FoodSoundCue.cs, uses this same instant full reveal rather than the slow
// probabilistic ambient-hearing one further down).
inline void RevealRadiusInstant(int cx, int cy, int radius, const WorldGrid& grid, AgentMemory& memory) {
    for (int dx = -radius; dx <= radius; ++dx) {
        for (int dy = -radius; dy <= radius; ++dy) {
            int tx = cx + dx;
            int ty = cy + dy;
            if (tx < 0 || tx >= grid.width || ty < 0 || ty >= grid.height) continue;

            memory.exploredTiles.at(ty, tx) = true;
            memory.discoveredWalls.at(ty, tx) = grid.wallLayer.at(ty, tx);
            memory.discoveredWood.at(ty, tx) = grid.woodLayer.at(ty, tx);
            memory.discoveredFood.at(ty, tx) = grid.foodLayer.at(ty, tx);
            memory.discoveredStone.at(ty, tx) = grid.stoneLayer.at(ty, tx);
            memory.discoveredWaterEdge.at(ty, tx) = grid.waterEdgeLayer.at(ty, tx);

            if (tx == grid.buildSiteX && ty == grid.buildSiteY) {
                memory.buildSiteX = tx;
                memory.buildSiteY = ty;
            }
            if (tx == grid.campX && ty == grid.campY) {
                memory.campX = tx;
                memory.campY = ty;
            }
        }
    }
}

inline void UpdatePhysicalSenses(int x, int y, const WorldGrid& grid, AgentMemory& memory, const AgentProfile& profile) {
    RevealRadiusInstant(x, y, profile.sightRadius, grid, memory);

    // Hearing, much farther than sight, ignores walls (models sound
    // travelling/rustling rather than line-of-sight). hearingRange <= 0
    // (Deaf) contributes nothing here — the actual "hearing impaired" rule.
    int hearing = profile.hearingRange;
    if (hearing > 0) {
        for (int dx = -hearing; dx <= hearing; ++dx) {
            for (int dy = -hearing; dy <= hearing; ++dy) {
                int tx = x + dx;
                int ty = y + dy;
                if (tx < 0 || tx >= grid.width || ty < 0 || ty >= grid.height) continue;

                // Food is a distinctive, precise sound (rustling/animals) —
                // heard immediately, every time, unlike general ambient noise.
                if (grid.foodLayer.at(ty, tx)) {
                    memory.discoveredFood.at(ty, tx) = true;
                }

                // Everything else about this tile is only picked up slowly,
                // from ambient sound in general — a small chance per call
                // instead of sight's instant, guaranteed reveal, so it takes
                // several passes nearby to build up the same knowledge.
                if (!memory.exploredTiles.at(ty, tx) && RandomChance(kHearingDiscoveryChance)) {
                    memory.exploredTiles.at(ty, tx) = true;
                    memory.discoveredWalls.at(ty, tx) = grid.wallLayer.at(ty, tx);
                    memory.discoveredWood.at(ty, tx) = grid.woodLayer.at(ty, tx);
                    memory.discoveredStone.at(ty, tx) = grid.stoneLayer.at(ty, tx);
                    memory.discoveredWaterEdge.at(ty, tx) = grid.waterEdgeLayer.at(ty, tx);
                }
            }
        }
    }
}
