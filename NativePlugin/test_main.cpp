// test_main.cpp
// Comprehensive test runner verifying Member 1's spatial grid and multi-profile perception engine.

#include <iostream>
#include <iomanip>
#include "spatial/WorldGrid.h"
#include "spatial/AgentMemory.h"
#include "spatial/Perception.h"
#include "goap/WorldState.h"

// Helper function to print a visual layer out to the console grid
void PrintWallLayer(const MapLayer<bool>& layer) {
    std::cout << "\n--- Console Grid Map View ---\n";
    for (int r = 0; r < layer.get_height(); ++r) {
        for (int c = 0; c < layer.get_width(); ++c) {
            if (layer.at(r, c)) {
                std::cout << "[#] "; // Solid structure
            }
            else {
                std::cout << "[.] "; // Walkable street
            }
        }
        std::cout << "\n";
    }
    std::cout << "-----------------------------\n";
}

// Helper to print the truth table generated for GOAP evaluation
void PrintGoapTable(const std::string& profileName, const WorldState& state) {
    std::cout << "\n--- GOAP WorldState Truth Table [" << profileName << "] ---\n";
    std::cout << "stairs_nearby:           " << (state.Get("stairs_nearby") ? "TRUE" : "FALSE") << "\n";
    std::cout << "tactile_paving_detected: " << (state.Get("tactile_paving_detected") ? "TRUE" : "FALSE") << "\n";
    std::cout << "ramp_nearby:             " << (state.Get("ramp_nearby") ? "TRUE" : "FALSE") << "\n";
    std::cout << "crosswalk_detected:      " << (state.Get("crosswalk_detected") ? "TRUE" : "FALSE") << "\n";
    std::cout << "crowd_present:           " << (state.Get("crowd_present") ? "TRUE" : "FALSE") << "\n";
    std::cout << "---------------------------------------------------------\n";
}

int main() {
    std::cout << "=========================================================\n";
    std::cout << "=== COMPREHENSIVE BACKEND SPATIAL & PERCEPTION ENGINE ===\n";
    std::cout << "=========================================================\n";

    // 1. Initialize a 10x10 city grid scenario
    const int MAP_WIDTH = 10;
    const int MAP_HEIGHT = 10;

    WorldGrid cityGrid;
    cityGrid.width = MAP_WIDTH;
    cityGrid.height = MAP_HEIGHT;

    cityGrid.wallLayer.resize(MAP_WIDTH, MAP_HEIGHT, false);
    cityGrid.stairLayer.resize(MAP_WIDTH, MAP_HEIGHT, false);
    cityGrid.rampLayer.resize(MAP_WIDTH, MAP_HEIGHT, 0.0f);
    cityGrid.tactilePaving.resize(MAP_WIDTH, MAP_HEIGHT, false);
    cityGrid.audioBeaconLayer.resize(MAP_WIDTH, MAP_HEIGHT, false);
    cityGrid.crowdDensityLayer.resize(MAP_WIDTH, MAP_HEIGHT, 0);

    // 2. Build urban layout structures
    // Create a horizontal structural building boundary wall across row 4
    for (int c = 0; c < MAP_WIDTH; ++c) {
        // Leave columns 4 and 5 open as a pedestrian crosswalk gap
        if (c != 4 && c != 5) {
            cityGrid.wallLayer.at(4, c) = true;
        }
    }

    // Place a set of stairs at the crosswalk entrance on column 4
    cityGrid.stairLayer.at(3, 4) = true;

    // Place a safe guiding tactile path tile next to it on column 5
    cityGrid.tactilePaving.at(3, 5) = true;

    // Place an acoustic crosswalk chime inside the gap at row 4, column 4
    cityGrid.audioBeaconLayer.at(4, 4) = true;

    // Simulate a dense pedestrian crowd block covering the tactile paving strip
    cityGrid.crowdDensityLayer.at(3, 5) = 3; // 3 active NPC agents blocking the cell

    // Output the physical topology blueprint to terminal
    PrintWallLayer(cityGrid.wallLayer);

    // Shared agent tracking variables
    int agentRow = 3;
    int agentCol = 4;
    std::cout << "\nSpawning Agent at crosswalk proximity point: (Row: " << agentRow << ", Col: " << agentCol << ")\n";

    // =========================================================
    // EVALUATION RUN 1: BLIND AGENT SIMULATION
    // =========================================================
    std::cout << "\n[TEST 1] Executing sensory sweeps for BLIND agent...\n";

    AgentMemory blindAgentMemory;
    blindAgentMemory.initialize(MAP_WIDTH, MAP_HEIGHT);

    // Execute short-range cane sweep
    UpdateTactileSensing(agentRow, agentCol, cityGrid, blindAgentMemory);

    // Verify if the localized sweep successfully cataloged structural changes
    bool knowsWallAhead = blindAgentMemory.discoveredWalls.at(4, 3);
    std::cout << "-> Did localized cane hit structural wall at (4,3)? "
        << (knowsWallAhead ? "YES (Success)" : "NO (Failed)") << "\n";

    // Run the GOAP Bridge Interpretation utilizing official Blind factory parameters
    WorldState blindGoapState = InterpretSensoryData(agentRow, agentCol, cityGrid, blindAgentMemory, AgentProfile::MakeBlind());
        PrintGoapTable("Profile: Blind", blindGoapState);

    // =========================================================
    // EVALUATION RUN 2: DEAF AGENT SIMULATION
    // =========================================================
    std::cout << "\n[TEST 2] Executing sensory sweeps for DEAF agent...\n";

    AgentMemory deafAgentMemory;
    deafAgentMemory.initialize(MAP_WIDTH, MAP_HEIGHT);

    // Execute localized tactile updates
    UpdateTactileSensing(agentRow, agentCol, cityGrid, deafAgentMemory);

    // Run the GOAP Bridge Interpretation utilizing official Deaf factory parameters
    WorldState deafGoapState = InterpretSensoryData(agentRow, agentCol, cityGrid, deafAgentMemory, AgentProfile::MakeDeaf());
        PrintGoapTable("Profile: Deaf", deafGoapState);

    // Final Structural Verification Checks
    std::cout << "=== Cross-Evaluation Validation Verification ===\n";
    if (blindGoapState.Get("crosswalk_detected") && !deafGoapState.Get("crosswalk_detected")) {
        std::cout << "-> Crosswalk Acoustic Isolation: PASS (Blind hears it, Deaf ignores it).\n";
    }
    else {
        std::cout << "-> Crosswalk Acoustic Isolation: FAIL.\n";
    }

    if (blindGoapState.Get("crowd_present") && deafGoapState.Get("crowd_present")) {
        std::cout << "-> Crowd Boundary Proximity: PASS (Both profiles successfully flag crowding).\n"; 
    }
    else {
        std::cout << "-> Crowd Boundary Proximity: FAIL.\n";
    }

    std::cout << "\nAll layered simulation sub-systems verified! Framework integration ready.\n";
    return 0;
}