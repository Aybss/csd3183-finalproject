using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to each palette icon (a UI Button/Image). Handles dragging
/// a ghost preview into the 3D scene and, on release over valid
/// ground, instantiating the real prefab and registering it with
/// the ObstacleGrid.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggablePropIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Which entry in the PropLibrary this icon represents.")]
    public PropEntry entry;

    [Tooltip("Camera used to raycast into the 3D scene. Defaults to Camera.main.")]
    public Camera sceneCamera;

    [Tooltip("Layer mask for the ground/placement surface.")]
    public LayerMask groundMask;

    [Tooltip("Snap placement to the ObstacleGrid cell centers.")]
    public bool snapToGrid = true;

    [Tooltip("Material applied to the ghost preview (should be semi-transparent).")]
    public Material ghostMaterial;

    private GameObject _ghostInstance;
    private bool _validDrop;
    private float _pivotToBottomOffset;

    private void Awake()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnBeginDrag fired. entry={(entry != null ? entry.displayName : "NULL")}, prefab={(entry?.prefab != null ? entry.prefab.name : "NULL")}, camera={(sceneCamera != null ? sceneCamera.name : "NULL")}");

        if (entry == null || entry.prefab == null) return;

        _ghostInstance = Instantiate(entry.prefab);
        _pivotToBottomOffset = GetPivotToBottomOffset(_ghostInstance);
        SetGhostVisuals(_ghostInstance);
        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_ghostInstance == null)
        {
            Debug.Log("[Drag] OnDrag fired but _ghostInstance is NULL (OnBeginDrag probably didn't create it).");
            return;
        }
        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnEndDrag fired. validDrop={_validDrop}, ghostExists={_ghostInstance != null}");

        if (_ghostInstance == null) return;

        if (_validDrop)
        {
            GameObject placed = Instantiate(entry.prefab, _ghostInstance.transform.position, _ghostInstance.transform.rotation);
            Debug.Log($"[Drag] Placed '{placed.name}' at {placed.transform.position}");
            if (ObstacleGrid.Instance != null)
                ObstacleGrid.Instance.RegisterProp(placed, placed.transform.position, entry);
            else
                Debug.LogWarning("[Drag] ObstacleGrid.Instance is NULL — no ObstacleGrid component found in the scene.");
        }
        else
        {
            Debug.LogWarning("[Drag] Drop was NOT valid — raycast never hit the ground layer during this drag.");
        }

        Destroy(_ghostInstance);
        _ghostInstance = null;
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        if (sceneCamera == null)
        {
            Debug.LogWarning("[Drag] sceneCamera is NULL — assign Scene Camera on the PaletteUI component.");
            _validDrop = false;
            return;
        }

        Ray ray = sceneCamera.ScreenPointToRay(eventData.position);
        _validDrop = Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask);
        Debug.Log($"[Drag] Raycast from {eventData.position}: hit={_validDrop}, mask={groundMask.value}" + (_validDrop ? $", hitObject={hit.collider.gameObject.name}, point={hit.point}" : ""));

        if (!_validDrop)
        {
            // Park it off-screen/below ground while there's no valid hit.
            _ghostInstance.SetActive(false);
            return;
        }

        _ghostInstance.SetActive(true);
        Vector3 worldPos = hit.point;

        if (snapToGrid && ObstacleGrid.Instance != null)
        {
            Vector2Int cell = ObstacleGrid.Instance.WorldToCell(worldPos);
            worldPos = ObstacleGrid.Instance.CellToWorld(cell);
        }

        worldPos.y = hit.point.y + _pivotToBottomOffset;

        _ghostInstance.transform.position = worldPos;
    }

    /// <summary>
    /// Returns how far the object's pivot sits above its actual visual
    /// bottom edge. Add this to a ground hit point's Y to make the
    /// object's base rest on the ground instead of its pivot.
    /// </summary>
    private float GetPivotToBottomOffset(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return go.transform.position.y - bounds.min.y;
    }

    private void SetGhostVisuals(GameObject go)
    {
        if (ghostMaterial == null) return;

        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
            renderer.materials = mats;
        }

        // Ghost previews shouldn't block their own raycast.
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }
}
