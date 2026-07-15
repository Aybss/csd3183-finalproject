using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggablePropIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PropEntry entry;
    public Camera sceneCamera;
    public LayerMask groundMask;
    public bool snapToGrid = true;
    public Material ghostMaterial;

    private GameObject _ghostInstance;
    private bool _validDrop;
    private float _pivotToBottomOffset;
    private Vector2Int _currentHoveredCell;

    private void Awake()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (entry == null || entry.prefab == null) return;

        FindObjectOfType<GridInteractionManager>()?.DisableDeleteMode();

        _ghostInstance = Instantiate(entry.prefab);
        _pivotToBottomOffset = GetPivotToBottomOffset(_ghostInstance);
        SetGhostVisuals(_ghostInstance);
        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_ghostInstance == null) return;
        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_ghostInstance == null) return;

        if (_validDrop)
        {
            if (ObstacleGrid.Instance.CanPlaceProp(_currentHoveredCell, entry.footprintSize))
            {
                Vector3 spawnPos = ObstacleGrid.Instance.GetFootprintCenterWorld(_currentHoveredCell, entry.footprintSize);
                spawnPos.y = _ghostInstance.transform.position.y;

                GameObject placed = Instantiate(entry.prefab, spawnPos, _ghostInstance.transform.rotation);
                ObstacleGrid.Instance.RegisterProp(placed, _currentHoveredCell, entry);
            }
        }

        if (GridVisualizer.Instance != null)
        {
            GridVisualizer.Instance.ClearPreview();
        }

        Destroy(_ghostInstance);
        _ghostInstance = null;
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        if (sceneCamera == null)
        {
            _validDrop = false;
            return;
        }

        Ray ray = sceneCamera.ScreenPointToRay(eventData.position);
        _validDrop = Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask);

        if (!_validDrop)
        {
            _ghostInstance.SetActive(false);
            if (GridVisualizer.Instance != null) GridVisualizer.Instance.ClearPreview();
            return;
        }

        _ghostInstance.SetActive(true);
        Vector3 worldPos = hit.point;

        _currentHoveredCell = ObstacleGrid.Instance.WorldToCell(worldPos);

        if (snapToGrid)
        {
            worldPos = ObstacleGrid.Instance.GetFootprintCenterWorld(_currentHoveredCell, entry.footprintSize);
        }

        worldPos.y = hit.point.y + _pivotToBottomOffset;
        _ghostInstance.transform.position = worldPos;

        if (GridVisualizer.Instance != null)
        {
            bool canPlace = ObstacleGrid.Instance.CanPlaceProp(_currentHoveredCell, entry.footprintSize);
            GridVisualizer.Instance.UpdatePreview(_currentHoveredCell, entry.footprintSize, canPlace);
        }
    }

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

        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }
}