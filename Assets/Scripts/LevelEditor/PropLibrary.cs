using UnityEngine;

/// <summary>
/// A single placeable prop entry shown in the palette.
/// Includes accessibility metadata used later when this prop is
/// registered into the obstacle grid for hazard-aware pathfinding.
/// </summary>
[System.Serializable]
public class PropEntry
{
    public string displayName;
    public GameObject prefab;
    public Sprite icon;

    [Tooltip("Footprint size in grid cells (X, Z).")]
    public Vector2Int footprintSize = Vector2Int.one;

    [Header("Accessibility / Hazard Flags")]
    [Tooltip("Blocks wheelchair navigation (e.g. curbs, stairs, narrow gaps).")]
    public bool isWheelchairObstacle;

    [Tooltip("Should trigger an audio cue when a visually impaired user approaches (e.g. crosswalk, hazard).")]
    public bool needsAudioCue;

    [Tooltip("Should trigger a strong visual highlight for hearing-impaired users (e.g. flashing alert instead of a siren/alarm sound).")]
    public bool needsVisualCue;
}

/// <summary>
/// Create via Assets > Create > Pathfinder > Prop Library.
/// Populate this with your Kenney City Kit Suburban prefabs.
/// </summary>
[CreateAssetMenu(fileName = "PropLibrary", menuName = "Pathfinder/Prop Library")]
public class PropLibrary : ScriptableObject
{
    public PropEntry[] props;
}
