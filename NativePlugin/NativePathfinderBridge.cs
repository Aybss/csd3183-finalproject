// NativePathfinderBridge.cs
using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativePathfinderBridge : MonoBehaviour 
{
    const string DLL_NAME = "AccessibilitySimulation"; // Name of your compiled C++ DLL

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void InitializeGrid(int width, int height);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetWallData(bool[] flatWallArray);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RequestPath(int startR, int startC, int endR, int endC, int profileType, int[] outBuffer);

    public void SetupBackendMap(int width, int height, bool[] walls) 
    {
        InitializeGrid(width, height);
        SetWallData(walls);
        Debug.Log("C++ Backend map initialized!");
    }

    public Vector2Int[] GetAgentPath(Vector2Int start, Vector2Int end, int profileType) 
    {
        int[] buffer = new int[2000]; // Allocate memory for C++ to write into
        int pathLength = RequestPath(start.y, start.x, end.y, end.x, profileType, buffer);

        Vector2Int[] finalPath = new Vector2Int[pathLength];
        for (int i = 0; i < pathLength; i++) 
        {
            // Unpack the flat [row, col, row, col] array back into Vectors
            finalPath[i] = new Vector2Int(buffer[i * 2 + 1], buffer[i * 2]); 
        }
        return finalPath;
    }
}