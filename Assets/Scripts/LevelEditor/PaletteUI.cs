using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PaletteUI : MonoBehaviour
{
    public PropLibrary library;
    public GameObject iconButtonPrefab;
    public Transform contentParent;

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
            if (child.name != "DeleteModeButton" && child.name != "SelectModeButton" && child.name != "PanModeButton")
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

            if (go.TryGetComponent(out DraggablePropIcon oldDrag))
            {
                Destroy(oldDrag);
            }

            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();

            PropEntry currentEntry = entry;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                GridInteractionManager interactionManager = FindObjectOfType<GridInteractionManager>();
                if (interactionManager != null)
                {
                    interactionManager.SetSelectedProp(currentEntry);
                    interactionManager.SetPanMode(false);
                    FindObjectOfType<CameraController>()?.TogglePanMode(false);
                }
            });
        }
    }
}