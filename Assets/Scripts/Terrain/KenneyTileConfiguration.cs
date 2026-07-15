using UnityEngine;

[System.Serializable]
public struct SpawnConfiguration
{
    public GameObject prefab;
    [Range(0f, 1f)] public float density;
    public float yOffset;
}

[System.Serializable]
public struct KenneyTileConfiguration
{
    [Header("Core Floor Surfaces")]
    public GameObject grassCenter;

    [Header("River Transitions & Offsets")]
    public SpawnConfiguration waterCenter;
    public SpawnConfiguration ground_riverSide;
    public SpawnConfiguration ground_riverCorner;
    public SpawnConfiguration ground_riverSideOpen;

    [Header("Linear Infrastructure & Fences")]
    public SpawnConfiguration fence;
    public SpawnConfiguration fence_corner;
    public SpawnConfiguration path;
    public SpawnConfiguration bridge_wood;

    [Header("Camps & Installations")]
    public SpawnConfiguration tent_a;
    public SpawnConfiguration tent_b;
    public SpawnConfiguration statue_obelisk;
    public SpawnConfiguration log_stack;

    [Header("Flora Resources")]
    public SpawnConfiguration bush;
    public SpawnConfiguration mushroom;
    public SpawnConfiguration crop_wheat;
    public SpawnConfiguration crop_berries;
    public SpawnConfiguration tree_large;
    public SpawnConfiguration tree_small;
    public SpawnConfiguration tree_tall;

    [Header("Resources")]
    public SpawnConfiguration food;

    [Header("Stone & Geological Items")]
    public SpawnConfiguration platform_grass;
    public SpawnConfiguration rock_small_a;
    public SpawnConfiguration rock_small_b;
    public SpawnConfiguration rock_tall_a;
    public SpawnConfiguration rock_tall_b;
    public SpawnConfiguration stone_large_a;
    public SpawnConfiguration stone_large_b;
}