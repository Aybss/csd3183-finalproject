using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates a scrollable side panel with one draggable icon per
/// entry in the assigned PropLibrary. Attach to an empty GameObject
/// and assign the "Content" transform of a ScrollView as the parent.
/// </summary>
public class PaletteUI : MonoBehaviour
{
    public PropLibrary library;

    [Tooltip("Prefab with an Image + DraggablePropIcon component (see setup notes).")]
    public GameObject iconButtonPrefab;

    [Tooltip("Parent transform (e.g. ScrollView/Viewport/Content).")]
    public Transform contentParent;

    public Camera sceneCamera;
    public LayerMask groundMask;
    public Material ghostMaterial;

    private void Start()
    {
        Populate();
    }

    public void Populate()
    {
        if (library == null || iconButtonPrefab == null || contentParent == null)
        {
            Debug.LogWarning("PaletteUI is missing a required reference.");
            return;
        }

        foreach (Transform child in contentParent)
        {
            if (child.name != "DeleteModeButton")
            {
                Destroy(child.gameObject);
            }
        }

        foreach (var entry in library.props)
        {
            GameObject go = Instantiate(iconButtonPrefab, contentParent);
            go.name = "Icon_" + entry.displayName;

            go.transform.SetAsLastSibling();

            Image img = go.GetComponentInChildren<Image>();
            if (img != null && entry.icon != null) img.sprite = entry.icon;

            TMP_Text tmpLabel = go.GetComponentInChildren<TMP_Text>();
            if (tmpLabel != null)
            {
                tmpLabel.text = entry.displayName;
                tmpLabel.alignment = TextAlignmentOptions.Bottom;
                tmpLabel.enableWordWrapping = true;
                tmpLabel.overflowMode = TextOverflowModes.Overflow;
                tmpLabel.enableAutoSizing = true;
                tmpLabel.fontSizeMin = 8f;
                tmpLabel.fontSizeMax = 14f;
            }
            else
            {
                Text label = go.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = entry.displayName;
                    label.alignment = TextAnchor.LowerCenter;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }

            DraggablePropIcon drag = go.GetComponent<DraggablePropIcon>();
            if (drag == null) drag = go.AddComponent<DraggablePropIcon>();

            drag.entry = entry;
            drag.sceneCamera = sceneCamera != null ? sceneCamera : Camera.main;
            drag.groundMask = groundMask;
            drag.ghostMaterial = ghostMaterial;
        }
    }
}