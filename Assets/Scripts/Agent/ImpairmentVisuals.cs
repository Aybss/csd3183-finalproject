using UnityEngine;

// Mirrors PathfinderCore/goap's AgentRole (0=WheelchairBound, 1=Blind, 2=Deaf).
public enum AgentRoleType
{
    WheelchairBound = 0,
    Blind = 1,
    Deaf = 2,
}

// Builds each agent's role visuals procedurally from primitives — the bundled
// Kenney asset packs have no character/wheelchair models, so this is color +
// attached shapes instead of custom art:
//   WheelchairBound: blue tint, flattened "wheel disc" base, squashed torso.
//   Blind:           red tint, dark blindfold band across the head.
//   Deaf:            orange tint, two ear-cover spheres.
// Role, action, hunger, and fatigue are all shown in the AgentOverlayUI
// screen-space panel instead of a floating world-space label.
public static class ImpairmentVisuals
{
    private static readonly Color WheelchairColor = new Color(0.25f, 0.45f, 0.95f);
    private static readonly Color BlindColor = new Color(0.85f, 0.2f, 0.2f);
    private static readonly Color DeafColor = new Color(0.95f, 0.6f, 0.1f);
    private static readonly Color AccessoryColor = new Color(0.08f, 0.08f, 0.08f);

    public static void Apply(GameObject agentObject, AgentRoleType role, float cellSize)
    {
        Color roleColor = ColorForRole(role);
        TintBody(agentObject, roleColor);

        float height = GetCapsuleHeight(agentObject);

        switch (role)
        {
            case AgentRoleType.WheelchairBound:
                AddWheelDisc(agentObject, roleColor);
                agentObject.transform.localScale = new Vector3(agentObject.transform.localScale.x, agentObject.transform.localScale.y * 0.85f, agentObject.transform.localScale.z);
                break;
            case AgentRoleType.Blind:
                AddBlindfold(agentObject, height);
                break;
            case AgentRoleType.Deaf:
                AddEarCovers(agentObject, height);
                break;
        }

        AddSightRing(agentObject, roleColor, height, SightRadiusForRole(role) * cellSize);
        AddStatusBars(agentObject, height);

        agentObject.AddComponent<AgentDeathVisual>();
    }

    // Mirrors PathfinderCore/goap/WorldState.h's AgentProfile factories
    // (MakeBlind/MakeWheelchairUser/MakeDeaf sightRadius) — this duplication
    // is purely cosmetic for the ring; the real vision/SLAM sweep is computed
    // natively via AgentPerceive using those same numbers.
    private static int SightRadiusForRole(AgentRoleType role)
    {
        switch (role)
        {
            case AgentRoleType.Blind: return 1;
            case AgentRoleType.WheelchairBound: return 5;
            case AgentRoleType.Deaf: return 7;
            default: return 5;
        }
    }

    private static Color ColorForRole(AgentRoleType role)
    {
        switch (role)
        {
            case AgentRoleType.WheelchairBound: return WheelchairColor;
            case AgentRoleType.Blind: return BlindColor;
            case AgentRoleType.Deaf: return DeafColor;
            default: return Color.white;
        }
    }

    // Public so AgentOverlayUI's screen-space list can show the same names.
    public static string LabelForRole(AgentRoleType role)
    {
        switch (role)
        {
            case AgentRoleType.WheelchairBound: return "Wheelchair";
            case AgentRoleType.Blind: return "Blind";
            case AgentRoleType.Deaf: return "Deaf";
            default: return "Agent";
        }
    }

    private static float GetCapsuleHeight(GameObject agentObject)
    {
        CapsuleCollider capsule = agentObject.GetComponent<CapsuleCollider>();
        return capsule != null ? capsule.height : 2f;
    }

    private static void TintBody(GameObject agentObject, Color color)
    {
        foreach (Renderer renderer in agentObject.GetComponentsInChildren<Renderer>())
        {
            Material tinted = renderer.sharedMaterial != null
                ? new Material(renderer.sharedMaterial)
                : new Material(Shader.Find("Standard"));
            tinted.color = color;
            renderer.material = tinted;
        }
    }

    private static GameObject AddPrimitiveChild(GameObject parent, PrimitiveType type, string name, Color color, Vector3 localPos, Vector3 localScale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.Destroy(part.GetComponent<Collider>()); // purely cosmetic, no extra physics
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.material = mat;

        return part;
    }

    private static void AddWheelDisc(GameObject agentObject, Color color)
    {
        // A wide, flat cylinder under the agent reads as a wheelchair base
        // without needing an actual wheelchair mesh.
        AddPrimitiveChild(agentObject, PrimitiveType.Cylinder, "WheelDisc", color,
            new Vector3(0f, -0.4f, 0f), new Vector3(0.9f, 0.08f, 0.9f));
    }

    private static void AddBlindfold(GameObject agentObject, float height)
    {
        AddPrimitiveChild(agentObject, PrimitiveType.Cube, "Blindfold", AccessoryColor,
            new Vector3(0f, height * 0.38f, 0.42f), new Vector3(0.55f, 0.14f, 0.12f));
    }

    private static void AddEarCovers(GameObject agentObject, float height)
    {
        AddPrimitiveChild(agentObject, PrimitiveType.Sphere, "EarCoverLeft", AccessoryColor,
            new Vector3(-0.45f, height * 0.4f, 0f), new Vector3(0.22f, 0.22f, 0.22f));
        AddPrimitiveChild(agentObject, PrimitiveType.Sphere, "EarCoverRight", AccessoryColor,
            new Vector3(0.45f, height * 0.4f, 0f), new Vector3(0.22f, 0.22f, 0.22f));
    }

    // A thin ground-level ring showing how far this agent can actually see —
    // makes the sensing difference between roles (Blind's tiny radius vs.
    // Deaf's wide one) immediately visible, and doubles as an intuitive
    // stand-in for "this is roughly the area SLAM adds to its memory as it
    // walks around."
    private static void AddSightRing(GameObject agentObject, Color color, float capsuleHeight, float radius)
    {
        GameObject ringObj = new GameObject("SightRing");
        ringObj.transform.SetParent(agentObject.transform, false);
        ringObj.transform.localPosition = new Vector3(0f, -capsuleHeight * 0.5f + 0.05f, 0f);

        const int segments = 40;
        LineRenderer line = ringObj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.widthMultiplier = 0.08f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Vector3[] points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            points[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
        line.SetPositions(points);
    }

    private static void AddStatusBars(GameObject agentObject, float height)
    {
        AgentStats stats = agentObject.GetComponent<AgentStats>();
        if (stats == null) return;

        GameObject barsAnchor = new GameObject("StatusBarsAnchor");
        ScaleCompensation.Attach(barsAnchor, agentObject.transform, Vector3.zero);

        AgentStatusBars bars = barsAnchor.AddComponent<AgentStatusBars>();
        bars.Initialize(stats, height * 0.75f + 0.25f);
    }
}

// Keeps a role label readable from any camera angle.
public class BillboardToCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main == null) return;
        transform.forward = transform.position - Camera.main.transform.position;
    }
}
