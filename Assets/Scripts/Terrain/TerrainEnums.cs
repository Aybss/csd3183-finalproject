namespace ProceduralTerrain
{
    public enum BiomeType
    {
        Grass,
        Water,
        Wood,
        Food,
        Stone
    }

    // Explicit asset catalog index to track exactly what prop is sitting on a tile
    public enum PropObjectType
    {
        None = -1,
        Path = 0,
        Campfire = 1,
        TentA = 2,
        TentB = 3,
        StatueObelisk = 4,
        LogStack = 5,
        Bush = 6,
        Mushroom = 7,
        CropWheat = 8,
        CropBerries = 9,
        TreeLarge = 10,
        TreeSmall = 11,
        TreeTall = 12,
        PlatformGrass = 13,
        RockSmallA = 14,
        RockSmallB = 15,
        RockTallA = 16,
        RockTallB = 17,
        StoneLargeA = 18,
        StoneLargeB = 19,
        Fence = 20,
        FenceCorner = 21,
        BridgeWood = 22
    }
}