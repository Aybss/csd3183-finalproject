using UnityEngine;
using TMPro;

// Mirrors PathfinderCore/goap's AgentRole (0=WheelchairBound, 1=Blind, 2=Deaf).
public enum AgentRoleType
{
    WheelchairBound = 0,
    Blind = 1,
    Deaf = 2,
}

// Builds each agent's role visuals procedurally from primitives — the bundled
// Kenney asset packs have no character/wheelchair models, so this is color +
// attached shapes + a floating label instead of custom art:
//   WheelchairBound: blue tint, flattened "wheel disc" base, squashed torso.
//   Blind:           red tint, dark blindfold band across the head.
//   Deaf:            orange tint, two ear-cover spheres.
// Every agent also gets a world-space label showing its role and current
// GOAP action, updated live from AgentGOAP.
public static class ImpairmentVisuals
{
    private static readonly Color WheelchairColor = new Color(0.25f, 0.45f, 0.95f);
    private static readonly Color BlindColor = new Color(0.85f, 0.2f, 0.2f);
    private static readonly Color DeafColor = new Color(0.95f, 0.6f, 0.1f);
    private static readonly Color AccessoryColor = new Color(0.08f, 0.08f, 0.08f);

    public static void Apply(GameObject agentObject, AgentRoleType role)
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

        AddLabel(agentObject, role, height);
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

    private static string LabelForRole(AgentRoleType role)
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

    private static void AddLabel(GameObject agentObject, AgentRoleType role, float height)
    {
        GameObject labelObj = new GameObject("RoleLabel");
        labelObj.transform.SetParent(agentObject.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, height * 0.75f + 0.6f, 0f);

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = LabelForRole(role);
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);

        labelObj.AddComponent<BillboardToCamera>();

        AgentGOAP goap = agentObject.GetComponent<AgentGOAP>();
        if (goap != null)
        {
            RoleActionLabel updater = labelObj.AddComponent<RoleActionLabel>();
            updater.Initialize(tmp, goap, LabelForRole(role));
        }
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

// Refreshes the floating label with the agent's live GOAP action so it
// doubles as an in-scene debug readout during a demo.
public class RoleActionLabel : MonoBehaviour
{
    private TextMeshPro label;
    private AgentGOAP goap;
    private string roleName;
    private AgentAction lastAction;
    private bool initialized;

    public void Initialize(TextMeshPro label, AgentGOAP goap, string roleName)
    {
        this.label = label;
        this.goap = goap;
        this.roleName = roleName;
    }

    private void Update()
    {
        if (label == null || goap == null) return;
        if (initialized && goap.CurrentAction == lastAction) return;

        lastAction = goap.CurrentAction;
        initialized = true;
        label.text = $"{roleName}\n{lastAction}";
    }
}
