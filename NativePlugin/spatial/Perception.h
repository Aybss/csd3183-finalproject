// Perception.hpp
// Handles spatial updates to agent memory and translates spatial data 
// into symbolic facts for the GOAP WorldState container.

#pragma once
#include "spatial/WorldGrid.h"     
#include "spatial/AgentMemory.h"   
#include "goap/WorldState.h"   
#include <cmath>
#include <algorithm>

// Phase 4: Proximity sweep simulating tactile stick navigation or close-quarters hearing
inline void UpdateTactileSensing(int agentRow, int agentCol, const WorldGrid& world, AgentMemory& memory) {
    for (int r = -1; r <= 1; ++r) {
        for (int c = -1; c <= 1; ++c) {
            int targetR = agentRow + r;
            int targetC = agentCol + c;

            // Boundary safeguard
            if (targetR >= 0 && targetR < world.height && targetC >= 0 && targetC < world.width) {
                bool isWall = world.wallLayer.at(targetR, targetC);
                memory.discover_cell(targetR, targetC, isWall);
            }
        }
    }
}

// Phase 4: Standard integer raycast (Bresenham's line) to verify visual occlusion
inline bool CheckLineOfSight(int startR, int startC, int endR, int endC, const WorldGrid& world) {
    int dr = std::abs(endR - startR);
    int dc = std::abs(endC - startC);
    int r = startR;
    int c = startC;
    int n = 1 + dr + dc;
    int r_inc = (endR > startR) ? 1 : -1;
    int c_inc = (endC > startC) ? 1 : -1;
    int error = dc - dr;
    dr *= 2;
    dc *= 2;

    for (; n > 0; --n) {
        if (world.wallLayer.at(r, c)) {
            return false; // Path blocked by solid structural element
        }
        if (r == endR && c == endC) return true;

        if (error > 0) {
            c += c_inc;
            error -= dr;
        } else {
            r += r_inc;
            error += dc;
        }
    }
    return true;
}

// THE BRIDGE FUNCTION: Converts your spatial calculations into GOAP strings
inline WorldState InterpretSensoryData(int agentRow, int agentCol, const WorldGrid& world, const AgentMemory& memory, const AgentProfile& profile) {
    WorldState symbolicState;

    symbolicState.Set("ramp_nearby", false);
    symbolicState.Set("stairs_nearby", false);
    symbolicState.Set("tactile_paving_detected", false);
    symbolicState.Set("crowd_present", false);

    // Use your teammate's dynamic sightRadius parameter!
    int checkRadius = profile.sightRadius;

    for (int r = -checkRadius; r <= checkRadius; ++r) {
        for (int c = -checkRadius; c <= checkRadius; ++c) {
            int targetR = agentRow + r;
            int targetC = agentCol + c;

            if (targetR >= 0 && targetR < world.height && targetC >= 0 && targetC < world.width) {
                if (CheckLineOfSight(agentRow, agentCol, targetR, targetC, world)) {
                    if (world.stairLayer.at(targetR, targetC)) {
                        symbolicState.Set("stairs_nearby", true);
                    }
                    if (world.rampLayer.at(targetR, targetC) > 0.0f) {
                        symbolicState.Set("ramp_nearby", true);
                    }
                    if (world.tactilePaving.at(targetR, targetC)) {
                        symbolicState.Set("tactile_paving_detected", true);
                    }
                }
            }
        }
    }

    return symbolicState;
}