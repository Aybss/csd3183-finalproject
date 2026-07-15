using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativeBridge : MonoBehaviour
{
    [DllImport("PathfinderCore")]
    public static extern int InitializeGrid(int width, int height);

    [DllImport("PathfinderCore")]
    public static extern void SetCellBlocked(int x, int y, int blocked);

    [DllImport("PathfinderCore")]
    public static extern void LoadObstacleData(IntPtr data, int length);

    [DllImport("PathfinderCore")]
    public static extern int FindPath(
        int startX, int startY, int endX, int endY,
        int[] outX, int[] outY, int maxPathLength
    );

    [DllImport("PathfinderCore")]
    public static extern int GenerateAccessibilityPlan(int profileType, int[] outActionIndices, int maxPlanLength);
}