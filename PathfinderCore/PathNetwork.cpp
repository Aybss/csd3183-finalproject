#include "PathNetwork.h"
#include <cmath>
#include <limits>

std::vector<PathEdge> ComputePrimsMST(const std::vector<int>& xs, const std::vector<int>& ys)
{
    std::vector<PathEdge> edges;
    int count = static_cast<int>(xs.size());
    if (count < 2 || static_cast<int>(ys.size()) != count) return edges;

    std::vector<bool> inTree(count, false);
    std::vector<float> minEdgeCost(count, std::numeric_limits<float>::max());
    std::vector<int> minEdgeFrom(count, -1);

    // Start the tree at node 0.
    minEdgeCost[0] = 0.0f;

    for (int iteration = 0; iteration < count; ++iteration)
    {
        // Pick the cheapest-to-reach node still on the frontier.
        int best = -1;
        float bestCost = std::numeric_limits<float>::max();
        for (int i = 0; i < count; ++i)
        {
            if (!inTree[i] && minEdgeCost[i] < bestCost)
            {
                bestCost = minEdgeCost[i];
                best = i;
            }
        }

        if (best == -1) break; // disconnected (shouldn't happen for a full point set)

        inTree[best] = true;
        if (minEdgeFrom[best] != -1)
        {
            edges.push_back({ minEdgeFrom[best], best });
        }

        // Relax the frontier: any unvisited node closer to `best` than to
        // its previous nearest tree node updates its edge.
        for (int i = 0; i < count; ++i)
        {
            if (inTree[i]) continue;
            float dx = static_cast<float>(xs[i] - xs[best]);
            float dy = static_cast<float>(ys[i] - ys[best]);
            float dist = std::sqrt(dx * dx + dy * dy);
            if (dist < minEdgeCost[i])
            {
                minEdgeCost[i] = dist;
                minEdgeFrom[i] = best;
            }
        }
    }

    return edges;
}
