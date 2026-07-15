// spatial/Perception.h
//
// Turns raw WorldGrid ground-truth into one agent's subjective AgentMemory.
// Sight clears fog-of-war in a small radius (tiny for Blind agents); hearing
// reveals food from much farther away but only for agents with a positive
// hearingRange (Deaf agents get none). This is what makes SLAM knowledge
// genuinely different per role instead of just a smaller circle.
#pragma once
#include "spatial/WorldGrid.h"
#include "spatial/AgentMemory.h"
#include "goap/WorldState.h"

inline void UpdatePhysicalSenses(int x, int y, const WorldGrid& grid, AgentMemory& memory, const AgentProfile& profile) {
    int sight = profile.sightRadius;

    for (int dx = -sight; dx <= sight; ++dx) {
        for (int dy = -sight; dy <= sight; ++dy) {
            int tx = x + dx;
            int ty = y + dy;
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

    // Hearing: food only, much farther than sight, ignores walls (models
    // hearing rustling/animals rather than line-of-sight). hearingRange <= 0
    // (Deaf) contributes nothing here.
    int hearing = profile.hearingRange;
    if (hearing > 0) {
        for (int dx = -hearing; dx <= hearing; ++dx) {
            for (int dy = -hearing; dy <= hearing; ++dy) {
                int tx = x + dx;
                int ty = y + dy;
                if (tx < 0 || tx >= grid.width || ty < 0 || ty >= grid.height) continue;
                if (grid.foodLayer.at(ty, tx)) {
                    memory.discoveredFood.at(ty, tx) = true;
                }
            }
        }
    }
}
