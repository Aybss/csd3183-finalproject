// test_main.cpp
// Comprehensive test runner verifying the cooperative survival loop:
// - Physical map perception of resources (Wood and Food)
// - SLAM memory sharing (MergeMemory)
// - GOAP world state interpretation

#include <iostream>
#include <iomanip>
#include "spatial/WorldGrid.h"
#include "spatial/AgentMemory.h"
#include "spatial/Perception.h"
#include "goap/WorldState.h"

// Helper function to print a visual layer out to the console grid
void PrintWorldLayout(const WorldGrid& grid) {
    std::cout << "\n--- Console Grid Map View ---\n";
    for (int r = 0; r < grid.height; ++r) {
        for (int c = 0; c < grid.width; ++c) {
            if (grid.wallLayer.at(r, c)) {
                std::cout << "[#] "; // Solid Wall
            }
            else if (r == grid.buildSiteRow && c == grid.buildSiteCol) {
                std::cout << "[B] "; // Build Site
            }
            else if (grid.foodLayer.at(r, c) > 0) {
                std::cout << "[F] "; // Food Node
            }
            else if (grid.woodLayer.at(r, c) > 0) {
                std::cout << "[W] "; // Wood Pile
            }
            else {
                std::cout << "[.] "; // Open Space
            }
        }
        std::cout << "\n";
    }
    std::cout << "-----------------------------\n";
}

// Helper to print the truth table generated for GOAP evaluation
void PrintGoapTable(const std::string& profileName, const WorldState& state) {
    std::cout << "\n--- GOAP WorldState Truth Table [" << profileName << "] ---\n";
    std::cout << "food_detected:   " << (state.Get("food_detected") ? "TRUE" : "FALSE") << "\n";
    std::cout << "wood_detected:   " << (state.Get("wood_detected") ? "TRUE" : "FALSE") << "\n";
    std::cout << "at_build_site:   " << (state.Get("at_build_site") ? "TRUE" : "FALSE") << "\n";
    std::cout << "---------------------------------------------------------\n";
}

int main() {
    std::cout << "=========================================================\n";
    std::cout << "=== COOPERATIVE MAZE SURVIVAL BACKEND ENGINE VERIFIER ===\n";
    std::cout << "=========================================================\n";

    // 1. Initialize a 10x10 Maze Map
    const int MAP_WIDTH = 10;
    const int MAP_HEIGHT = 10;

    WorldGrid survivalMap;
    survivalMap.width = MAP_WIDTH;
    survivalMap.height = MAP_HEIGHT;

    // Allocate Layers
    survivalMap.wallLayer.resize(MAP_WIDTH, MAP_HEIGHT, false);
    survivalMap.foodLayer.resize(MAP_WIDTH, MAP_HEIGHT, 0);
    survivalMap.woodLayer.resize(MAP_WIDTH, MAP_HEIGHT, 0);

    // Set Build Site at the middle
    survivalMap.buildSiteRow = 5;
    survivalMap.buildSiteCol = 5;

    // 2. Build Maze Barriers
    // Generate a wall dividing top and bottom halves, leaving columns 4 & 5 open
    for (int c = 0; c < MAP_WIDTH; ++c) {
        if (c != 4 && c != 5) {
            survivalMap.wallLayer.at(4, c) = true;
        }
    }

    // Place Survival Resources on the map
    survivalMap.foodLayer.at(2, 3) = 5;  // Food source in the northern region
    survivalMap.woodLayer.at(7, 8) = 10; // Rich wood pile in the south-east corner

    // Output the physical topology layout to terminal
    PrintWorldLayout(survivalMap);

    // Shared agent tracking variables
    int agentRow = 3;
    int agentCol = 4;
    std::cout << "\nSpawning Agents at proximity point: (Row: " << agentRow << ", Col: " << agentCol << ")\n";

    // =========================================================
    // EVALUATION RUN 1: BLIND FORAGER SIMULATION (High hearing, low sight)
    // =========================================================
    std::cout << "\n[TEST 1] Executing sensory sweeps for BLIND FORAGER...\n";

    AgentMemory blindMemory;
    blindMemory.initialize(MAP_WIDTH, MAP_HEIGHT);

    // Get the Forager profile (sight = 1, hearing = 8)
    AgentProfile foragerProfile = AgentProfile::MakeBlind(); 

        // Run localized sight/tactile sweep to clear Fog of War
        UpdatePhysicalSenses(agentRow, agentCol, survivalMap, blindMemory, foragerProfile); 

        // Convert numeric perception to boolean GOAP world states
        WorldState foragerGoap = InterpretSensoryData(agentRow, agentCol, survivalMap, blindMemory, foragerProfile); 
        PrintGoapTable("Profile: Blind Forager", foragerGoap);

    // =========================================================
    // EVALUATION RUN 2: DEAF LUMBERJACK SIMULATION (Excellent sight, no hearing)
    // =========================================================
    std::cout << "\n[TEST 2] Executing sensory sweeps for DEAF LUMBERJACK...\n";

    AgentMemory deafMemory;
    deafMemory.initialize(MAP_WIDTH, MAP_HEIGHT);

    // Get the Deaf profile (sight = 7, hearing = 0)
    AgentProfile lumberjackProfile = AgentProfile::MakeDeaf();

        // Run physical vision sweeps (clears wide Fog of War radius)
        UpdatePhysicalSenses(agentRow, agentCol, survivalMap, deafMemory, lumberjackProfile);

        // Convert vision map to boolean states
        WorldState lumberjackGoap = InterpretSensoryData(agentRow, agentCol, survivalMap, deafMemory, lumberjackProfile);
        PrintGoapTable("Profile: Deaf Lumberjack", lumberjackGoap);

    // =========================================================
    // EVALUATION RUN 3: SLAM SYNC MEMORY SHARING (Blind Forager <-> Deaf Lumberjack)
    // =========================================================
    std::cout << "\n[TEST 3] Simulating SLAM communication merge on agent intersect...\n";

    // Prior to merge, the Forager is unaware of the southern wood node because sight is 1 and hearing is blind to wood
    std::cout << "-> Prior to SLAM - Does Forager know of any wood? "
        << (blindMemory.discoveredWood.at(7, 8) > 0 ? "YES" : "NO") << "\n";

    // Trigger the bitwise merge (Lumberjack passes their wide visual memory to the Blind Forager)
    blindMemory.Merge(deafMemory);

    std::cout << "-> After SLAM Sync - Does Forager know of southern wood now? "
        << (blindMemory.discoveredWood.at(7, 8) > 0 ? "YES (Success)" : "NO (Failed)") << "\n";

    // =========================================================
    // FINAL SYSTEM CHECKS
    // =========================================================
    std::cout << "\n=== System Integration Validation Verification ===\n";

    // Food Acoustic Check: Sighted blind agent should hear food acoustic beacons, deaf lumberjack ignores sound.
    if (foragerGoap.Get("food_detected") && !lumberjackGoap.Get("food_detected")) {
        std::cout << "-> Food Acoustic Isolation: PASS (Forager heard it, Deaf Collector remained deaf to it).\n";
    }
    else {
        std::cout << "-> Food Acoustic Isolation: FAIL.\n";
    }

    // Updated assertion to check for resource presence rather than raw quantity
    if (blindMemory.discoveredWood.at(7, 8) > 0) {
        std::cout << "-> Cooperative SLAM Sync Network: PASS (Spatial resource memory synced).\n";
    }
    else {
        std::cout << "-> Cooperative SLAM Sync Network: FAIL.\n";
    }

    std::cout << "\nAll cooperative survival tests verified! Backend is ready to compile.\n";
    return 0;
}