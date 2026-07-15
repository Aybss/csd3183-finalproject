cmake_minimum_required(VERSION 3.15)
project(AccessibilitySimulation LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 1. Collect all project source and header files across modular directories
set(PROJECT_SOURCES
    test_main.cpp
    api/PluginAPI.cpp
    core/AgentProfile.h
    spatial/MapLayer.h
    spatial/WorldGrid.h
    spatial/AgentMemory.h
    spatial/Perception.h
    navigation/AStarPathfinder.h
    goap/Action.h
    goap/AgentActions.h
    goap/GOAPPlanner.h
    goap/GOAPPlanner.cpp
    goap/WorldState.h
)

# 2. Compile as a Shared Library (.dll) for Unity integration
add_library(AccessibilitySimulation SHARED ${PROJECT_SOURCES})

# 3. Clean Include Mapping: Allows you to write #include "spatial/MapLayer.h" 
# from anywhere in the project without messy relative directory backtracking paths
target_include_directories(AccessibilitySimulation PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

# 4. Optional: Create an executable target for your local console testing
add_executable(BackendTest test_main.cpp ${PROJECT_SOURCES})
target_include_directories(BackendTest PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})