#pragma once
#include <vector>

struct PathEdge
{
    int fromIndex;
    int toIndex;
};

// Genuine Prim's-algorithm minimum spanning tree over a small set of
// point-of-interest coordinates (camp, build site, resource cluster
// centroids). Unlike a maze-carving DFS or Perlin noise, this repeatedly
// grows a single connected tree by picking the cheapest edge from the
// tree's frontier to an unvisited node — the textbook definition of Prim's.
// Returns one edge per newly-added node (count-1 edges for `count` nodes).
std::vector<PathEdge> ComputePrimsMST(const std::vector<int>& xs, const std::vector<int>& ys);
