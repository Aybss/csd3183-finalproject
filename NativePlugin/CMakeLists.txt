cmake_minimum_required(VERSION 3.15)
# Renamed the project/solution file to match your new survival scope
project(CooperativeMazeSurvival LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 1. Collect all project source and header files across modular directories
set(PROJECT_SOURCES
    test_main.cpp
    api/PluginAPI.cpp
    spatial/MapLayer.h
    spatial/WorldGrid.h
    spatial/Perception.h
    # Member 2's pathfinding files placed under the navigation folder
    navigation/AStarGrid.h
    navigation/AStarGrid.cpp
    navigation/Agent.h
    navigation/Agent.cpp
    goap/Action.h
    goap/AgentActions.h
    goap/GOAPPlanner.h
    goap/GOAPPlanner.cpp
    goap/WorldState.h
)

# 2. Compile as a Shared Library (.dll) for Unity integration
# This generates CooperativeMazeSurvival.dll instead of AccessibilitySimulation.dll
add_library(CooperativeMazeSurvival SHARED ${PROJECT_SOURCES})

# 3. Clean Include Mapping: Allows you to write #include "spatial/MapLayer.h" 
# from anywhere in the project without messy relative directory backtracking paths
target_include_directories(CooperativeMazeSurvival PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

# 4. Create an executable target for your local console testing
add_executable(BackendTest test_main.cpp ${PROJECT_SOURCES})
target_include_directories(BackendTest PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})