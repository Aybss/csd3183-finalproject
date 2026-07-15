using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridSaveSystem : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Assign your Prop Library Asset containing prefabs matched with their displayNames.")]
    public PropLibrary propLibrary;

    [Header("Save Configuration")]
    [Tooltip("The name used when hitting save (will create level_01.json, level_02.json, etc.).")]
    public string defaultSavePrefix = "level";

    [Header("UI Menu References")]
    [Tooltip("The UI Panel or ScrollView GameObject containing the level selector.")]
    public GameObject levelMenuPanel;

    [Tooltip("The parent 'Content' transform of your ScrollView (Viewport -> Content).")]
    public Transform contentParent;

    [Tooltip("The Level Button prefab you created.")]
    public GameObject buttonPrefab;

    private string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, "Saves");

    private void Update()
    {
        // Press 'L' on your keyboard during play to force-open the load menu
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("[SaveSystem] Hotkey 'L' pressed - opening load menu.");
            OpenLoadMenu();
        }
    }

    private void Awake()
    {
        if (!Directory.Exists(SaveDirectoryPath))
        {
            Directory.CreateDirectory(SaveDirectoryPath);
        }

        // Keep the menu hidden on startup
        if (levelMenuPanel != null)
        {
            levelMenuPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Saves the current layout to a dynamically numbered JSON file (e.g., level_1.json).
    /// </summary>
    public void SaveLevel()
    {
        if (ObstacleGrid.Instance == null)
        {
            Debug.LogError("[SaveSystem] ObstacleGrid instance is missing!");
            return;
        }

        // Determine the next available file index
        int fileIndex = 1;
        string finalPath = Path.Combine(SaveDirectoryPath, $"{defaultSavePrefix}_{fileIndex:D2}.json");
        while (File.Exists(finalPath))
        {
            fileIndex++;
            finalPath = Path.Combine(SaveDirectoryPath, $"{defaultSavePrefix}_{fileIndex:D2}.json");
        }

        GridSaveData layout = ObstacleGrid.Instance.GetSaveData();
        string json = JsonUtility.ToJson(layout, true);

        File.WriteAllText(finalPath, json);
        Debug.Log($"[SaveSystem] Level successfully saved to: {finalPath}");
    }

    /// <summary>
    /// Shows the selection menu panel and populates it with your saved level files.
    /// Bind this function to your Main Canvas 'Load' button!
    /// </summary>
    public void OpenLoadMenu()
    {
        if (levelMenuPanel == null)
            Debug.LogError($"[SaveSystem] 'Level Menu Panel' is not assigned on '{gameObject.name}'!", gameObject);
        if (contentParent == null)
            Debug.LogError($"[SaveSystem] 'Content Parent' is not assigned on '{gameObject.name}'!", gameObject);
        if (buttonPrefab == null)
            Debug.LogError($"[SaveSystem] 'Button Prefab' is not assigned on '{gameObject.name}'!", gameObject);

        if (levelMenuPanel == null || contentParent == null || buttonPrefab == null)
            return;

        // Open panel
        levelMenuPanel.SetActive(true);

        // Clear out old UI button objects
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // Search directory for saved map layouts
        string[] files = Directory.GetFiles(SaveDirectoryPath, "*.json");

        if (files.Length == 0)
        {
            Debug.LogWarning("[SaveSystem] No saved levels found in: " + SaveDirectoryPath);
            return;
        }

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            string levelNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

            // Spawn button
            GameObject btnObj = Instantiate(buttonPrefab, contentParent);
            btnObj.name = "Btn_" + levelNameWithoutExt;

            // Set the visual text on the button
            TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = levelNameWithoutExt.Replace("_", " ").ToUpper();
            }

            // Bind the click action dynamically using lambda functions
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => {
                    ExecuteLoad(filePath);
                    levelMenuPanel.SetActive(false); // Auto-hide menu on click
                });
            }
        }
    }

    /// <summary>
    /// Closes the selection window without loading anything.
    /// </summary>
    public void CloseLoadMenu()
    {
        if (levelMenuPanel != null)
        {
            levelMenuPanel.SetActive(false);
        }
    }

    private void ExecuteLoad(string filePath)
    {
        if (ObstacleGrid.Instance == null || propLibrary == null) return;

        ObstacleGrid.Instance.ClearGrid();

        string json = File.ReadAllText(filePath);
        GridSaveData layout = JsonUtility.FromJson<GridSaveData>(json);

        foreach (var savedProp in layout.savedProps)
        {
            PropEntry entry = FindEntryInLibrary(savedProp.displayName);
            if (entry == null)
            {
                Debug.LogWarning($"[SaveSystem] Prop '{savedProp.displayName}' not found in registry.");
                continue;
            }

            Vector3 targetPosition = ObstacleGrid.Instance.GetFootprintCenterWorld(savedProp.baseCell, entry.footprintSize);
            GameObject newlySpawned = Instantiate(entry.prefab, targetPosition, Quaternion.identity);
            ObstacleGrid.Instance.RegisterProp(newlySpawned, savedProp.baseCell, entry);
        }

        Debug.Log($"[SaveSystem] Loaded: {Path.GetFileName(filePath)}");
    }

    private PropEntry FindEntryInLibrary(string displayName)
    {
        foreach (var entry in propLibrary.props)
        {
            if (entry.displayName == displayName)
            {
                return entry;
            }
        }
        return null;
    }
}