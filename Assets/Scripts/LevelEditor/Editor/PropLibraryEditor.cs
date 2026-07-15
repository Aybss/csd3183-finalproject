using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Adds an "Auto-Generate Icons From Prefabs" button to the PropLibrary
/// Inspector. Uses Unity's built-in asset thumbnail system to create a
/// PNG + Sprite for each prop's prefab, and assigns it automatically.
/// This file MUST live in a folder named exactly "Editor" anywhere
/// under Assets (Unity excludes it from the built game automatically).
/// </summary>
[CustomEditor(typeof(PropLibrary))]
public class PropLibraryEditor : Editor
{
    private Queue<PropEntry> _pending;
    private Dictionary<PropEntry, int> _attempts;
    private PropLibrary _library;
    private int _successCount;
    private bool _running;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PropLibrary library = (PropLibrary)target;

        GUILayout.Space(10);

        using (new EditorGUI.DisabledScope(_running))
        {
            if (GUILayout.Button(_running ? "Generating..." : "Auto-Generate Icons From Prefabs"))
            {
                StartGenerating(library);
            }
        }

        EditorGUILayout.HelpBox(
            "This runs across several Editor frames in the background. " +
            "Keep this Inspector visible while it works.",
            MessageType.Info);
    }

    private void StartGenerating(PropLibrary library)
    {
        _library = library;
        _pending = new Queue<PropEntry>(library.props.Where(p => p.prefab != null));
        _attempts = new Dictionary<PropEntry, int>();
        _successCount = 0;
        _running = true;

        EditorApplication.update -= ProcessQueue;
        EditorApplication.update += ProcessQueue;
    }

    private void ProcessQueue()
    {
        // Keep the Inspector repainting — this is what actually lets
        // Unity's internal preview renderer make progress each frame.
        Repaint();

        if (_pending.Count == 0)
        {
            EditorApplication.update -= ProcessQueue;
            _running = false;
            EditorUtility.SetDirty(_library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PropLibrary] Generated {_successCount} icons.");
            Repaint();
            return;
        }

        var entry = _pending.Peek();
        Texture2D preview = AssetPreview.GetAssetPreview(entry.prefab);

        if (preview != null)
        {
            SaveIcon(entry, preview);
            _successCount++;
            _pending.Dequeue();
            return;
        }

        // Not ready yet this frame — count attempts, give up after ~5 seconds.
        _attempts.TryGetValue(entry, out int count);
        count++;
        _attempts[entry] = count;

        if (count > 300) // ~300 update ticks
        {
            Debug.LogWarning($"[PropLibrary] Timed out waiting for a preview of '{entry.prefab.name}'. Try running the generator again on its own.");
            _pending.Dequeue();
        }
    }

    private void SaveIcon(PropEntry entry, Texture2D preview)
    {
        const string folder = "Assets/Scripts/LevelEditor/GeneratedIcons";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/LevelEditor"))
                AssetDatabase.CreateFolder("Assets/Scripts", "LevelEditor");
            AssetDatabase.CreateFolder("Assets/Scripts/LevelEditor", "GeneratedIcons");
        }

        Texture2D readable = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
        RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height);
        Graphics.Blit(preview, rt);
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        readable.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);

        byte[] png = readable.EncodeToPNG();
        string path = $"{folder}/{entry.prefab.name}_icon.png";
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        entry.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
