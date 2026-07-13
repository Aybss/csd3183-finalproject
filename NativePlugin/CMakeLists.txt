cmake_minimum_required(VERSION 3.15)
project(GOAPTest LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)

add_executable(GOAPTest
    test_main.cpp
    goap/GOAPPlanner.cpp
)

target_include_directories(GOAPTest PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
