using UnityEngine;

// Attaches a child to `parent` so it tracks the parent's position (following
// a moving agent, or sitting above a building) while staying immune to the
// parent's own scale. Without this, a world-space bar/label parented under
// an object that's been scaled up or down for its own visual reasons (a
// building prefab imported at a different unit scale than the character
// controller, for instance) silently ends up squashed, stretched, or
// displaced far from where it was meant to sit — often out of view entirely.
public static class ScaleCompensation
{
    public static void Attach(GameObject child, Transform parent, Vector3 desiredWorldOffset)
    {
        child.transform.SetParent(parent, false);

        Vector3 parentScale = parent.lossyScale;
        Vector3 safeScale = new Vector3(
            SafeDivisor(parentScale.x),
            SafeDivisor(parentScale.y),
            SafeDivisor(parentScale.z));

        child.transform.localScale = new Vector3(1f / safeScale.x, 1f / safeScale.y, 1f / safeScale.z);
        child.transform.localPosition = new Vector3(
            desiredWorldOffset.x / safeScale.x,
            desiredWorldOffset.y / safeScale.y,
            desiredWorldOffset.z / safeScale.z);
    }

    private static float SafeDivisor(float value)
    {
        return Mathf.Approximately(value, 0f) ? 1f : value;
    }
}
