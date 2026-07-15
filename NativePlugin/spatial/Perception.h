// spatial/Perception.h
#pragma once
#include "spatial/WorldGrid.h"
#include "spatial/AgentMemory.h"
#include "goap/WorldState.h"
#include <string>

enum class AgentRole {
    FORAGER,    // Blind
    LUMBERJACK, // Deaf
    BUILDER     // Wheelchair
};

struct AgentProfile {
    AgentRole role;
    int sightRadius;
    int hearingRange;
    float fatigueRate; // Lumberjack drains fastest, Builder slowest
};

// Tactical localized sweep (bumping into walls/objects and clearing Fog of War)
inline void UpdatePhysicalSenses(int r, int c, const WorldGrid& grid, AgentMemory& memory, const AgentProfile& profile) {
    int radius = profile.sightRadius;

    for (int dr = -radius; dr <= radius; ++dr) {
        for (int dc = -radius; dc <= radius; ++dc) {
            int targetR = r + dr;
            int targetC = c + dc;

            // Bounds check
            if (targetR >= 0 && targetR < grid.height && targetC >= 0 && targetC < grid.width) {

                // 1. CLEAR THE FOG OF WAR
                memory.exploredTiles.at(targetR, targetC) = true;

                // 2. RECORD WALLS
                if (grid.wallLayer.at(targetR, targetC)) {
                    memory.discoveredWalls.at(targetR, targetC) = true;
                }

                // 3. RECORD RESOURCES (Only if they exist)
                memory.discoveredWood.at(targetR, targetC) = grid.woodLayer.at(targetR, targetC);
                memory.discoveredFood.at(targetR, targetC) = grid.foodLayer.at(targetR, targetC);

                // 4. RECORD BUILD SITE
                if (targetR == grid.buildSiteRow && targetC == grid.buildSiteCol) {
                    memory.buildSiteRow = targetR;
                    memory.buildSiteCol = targetC;
                }
            }
        }
    }
}

// Interpret sensory data for the decision pipeline
inline WorldState InterpretSensoryData(int agentRow, int agentCol, const WorldGrid& world, const AgentMemory& memory, const AgentProfile& profile) {
    WorldState state;

    // Default world states
    state.Set("food_detected", false);
    state.Set("wood_detected", false);
    state.Set("at_build_site", (agentRow == world.buildSiteRow && agentCol == world.buildSiteCol));

    // 1. Audio check for food (Only roles with hearing > 0 can detect food from a distance)
    if (profile.hearingRange > 0) {
        for (int r = -profile.hearingRange; r <= profile.hearingRange; ++r) {
            for (int c = -profile.hearingRange; c <= profile.hearingRange; ++c) {
                int targetR = agentRow + r;
                int targetC = agentCol + c;
                if (targetR >= 0 && targetR < world.height && targetC >= 0 && targetC < world.width) {
                    if (world.foodLayer.at(targetR, targetC) > 0) {
                        state.Set("food_detected", true);
                        // Also record in memory since we heard it
                        const_cast<AgentMemory&>(memory).discoveredFood.at(targetR, targetC) = true;
                    }
                }
            }
        }
    }

    // 2. Memory/Visual check for Wood
    for (int r = 0; r < world.height; ++r) {
        for (int c = 0; c < world.width; ++c) {
            if (memory.discoveredWood.at(r, c) && world.woodLayer.at(r, c) > 0) {
                state.Set("wood_detected", true);
            }
        }
    }

    return state;
}