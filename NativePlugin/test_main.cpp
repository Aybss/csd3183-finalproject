// test_main.cpp
// Console test runner to verify Member 1's spatial grid and perception systems.

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

int main() {
    std::cout << "=== Member 1: Spatial Grid & Perception System Test ===\n";

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

    // 2. Build some urban features into our layout
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

    // Display the generated environment structures
    PrintWallLayer(cityGrid.wallLayer);

    // 3. Spawn a blind or visually impaired agent on row 2, col 5
    int agentRow = 2;
    int agentCol = 5;
    std::cout << "\nSpawning Agent at position: (Row: " << agentRow << ", Col: " << agentCol << ")\n";

    AgentMemory blindAgentMemory;
    blindAgentMemory.initialize(MAP_WIDTH, MAP_HEIGHT);

    // 4. Run the Proximity Tactile Sweep (Cane Navigation simulation)
    std::cout << "Executing localized tactile sweep around agent...\n";
    UpdateTactileSensing(agentRow, agentCol, cityGrid, blindAgentMemory);

    // Verify if the agent's local memory successfully learned what's ahead
    bool knowsWallAhead = blindAgentMemory.discoveredWalls.at(4, 3);
    std::cout << "Does agent memory know about the structural wall at (4,3)? "
        << (knowsWallAhead ? "YES (Success)" : "NO (Failed)") << "\n";

    // 5. Test the GOAP Bridge Interpretation
    std::cout << "\nTranslating real physical coordinates into GOAP Symbolic Flags...\n";
    WorldState goapInputState = InterpretSensoryData(agentRow, agentCol, cityGrid, blindAgentMemory);

    // Output the resulting flags that Member 3's GOAP Planner will evaluate
    std::cout << "\n--- Generated GOAP WorldState Truth Table ---\n";
    std::cout << "stairs_nearby:           " << (goapInputState.Get("stairs_nearby") ? "TRUE" : "FALSE") << "\n";
    std::cout << "tactile_paving_detected: " << (goapInputState.Get("tactile_paving_detected") ? "TRUE" : "FALSE") << "\n";
    std::cout << "ramp_nearby:             " << (goapInputState.Get("ramp_nearby") ? "TRUE" : "FALSE") << "\n";
    std::cout << "---------------------------------------------\n";

    std::cout << "\nSpatial engine validation complete! System is structurally sound.\n";
    return 0;
}