cmake_minimum_required(VERSION 3.15)

# This generates "CooperativeMazeSurvival.sln"
project(CooperativeMazeSurvival LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect all project source and header files
set(PROJECT_SOURCES
    test_main.cpp
    api/PluginAPI.cpp
    spatial/MapLayer.h
    spatial/WorldGrid.h
    spatial/Perception.h
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

# Compile as a Shared Library (Outputs "CooperativeMazeSurvival.dll")
add_library(CooperativeMazeSurvival SHARED ${PROJECT_SOURCES})

# Clean Include Mapping
target_include_directories(CooperativeMazeSurvival PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

# Create executable target for your local console testing
add_executable(BackendTest test_main.cpp ${PROJECT_SOURCES})
target_include_directories(BackendTest PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})