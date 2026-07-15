using UnityEngine;
using UnityEngine.EventSystems;

public class GridInteractionManager : MonoBehaviour
{
    [Tooltip("The physics layer applied to your scene floor geometry.")]
    public LayerMask groundMask;

    [Tooltip("The semi-transparent material used for the placement preview ghost.")]
    public Material ghostMaterial;

    private Camera _cam;
    private bool _isDeleteMode = false;
    private PropEntry _selectedProp = null;
    private GameObject _ghostInstance = null;
    private float _pivotToBottomOffset = 0f;
    private float _currentRotationAngle = 0f;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        HandleHoverVisuals();

        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateActiveTarget();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (_isDeleteMode)
                {
                    ProcessDeletionInput();
                }
                else if (_selectedProp != null)
                {
                    ProcessPlacementInput();
                }
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            ProcessDeletionInput();
        }
    }

    private void HandleHoverVisuals()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            Vector2Int hoveredCell = ObstacleGrid.Instance.WorldToCell(hit.point);

            if (_isDeleteMode)
            {
                ClearGhostInstance();
                if (GridVisualizer.Instance != null)
                {
                    GridVisualizer.Instance.UpdatePreview(hoveredCell, Vector2Int.one, false);
                }
            }
            else if (_selectedProp != null)
            {
                bool canPlace = ObstacleGrid.Instance.CanPlaceProp(hoveredCell, _selectedProp.footprintSize);

                if (GridVisualizer.Instance != null)
                {
                    GridVisualizer.Instance.UpdatePreview(hoveredCell, _selectedProp.footprintSize, canPlace);
                }

                UpdateGhostPosition(hoveredCell, hit.point);
            }
            else
            {
                ClearGhostInstance();
                if (GridVisualizer.Instance != null) GridVisualizer.Instance.ClearPreview();
            }
        }
        else
        {
            ClearGhostInstance();
            if (GridVisualizer.Instance != null) GridVisualizer.Instance.ClearPreview();
        }
    }

    /// <summary>
    /// Call this from your new UI "S" Button OnClick() event.
    /// Clears both placement selection and delete mode.
    /// </summary>
    public void EnableSelectMode()
    {
        _isDeleteMode = false;
        _selectedProp = null;
        ClearGhostInstance();
        if (GridVisualizer.Instance != null) GridVisualizer.Instance.ClearPreview();
        Debug.Log("[Interaction] Select Mode Enabled. No active tools.");
    }

    public void RotateActiveTarget()
    {
        if (_selectedProp != null && _ghostInstance != null)
        {
            _currentRotationAngle = (_currentRotationAngle + 90f) % 360f;
            _ghostInstance.transform.rotation = Quaternion.Euler(0f, _currentRotationAngle, 0f);
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            Vector2Int hoveredCell = ObstacleGrid.Instance.WorldToCell(hit.point);

            if (ObstacleGrid.Instance.IsInBounds(hoveredCell) && ObstacleGrid.Instance.IsCellOccupied(hoveredCell))
            {
                RaycastHit objectHit;
                if (Physics.Raycast(ray, out objectHit, 500f))
                {
                    if (((1 << objectHit.collider.gameObject.layer) & groundMask) == 0)
                    {
                        GameObject targetObject = objectHit.collider.gameObject;
                        while (targetObject.transform.parent != null && targetObject.transform.parent.gameObject.layer != gameObject.layer)
                        {
                            targetObject = targetObject.transform.parent.gameObject;
                        }

                        targetObject.transform.Rotate(0f, 90f, 0f);
                    }
                }
            }
        }
    }

    public void SetSelectedProp(PropEntry entry)
    {
        _isDeleteMode = false;
        _selectedProp = entry;
        _currentRotationAngle = 0f;

        ClearGhostInstance();

        if (_selectedProp != null && _selectedProp.prefab != null)
        {
            _ghostInstance = Instantiate(_selectedProp.prefab);
            _pivotToBottomOffset = GetPivotToBottomOffset(_ghostInstance);
            SetGhostVisuals(_ghostInstance);
        }
    }

    public void EnableDeleteMode()
    {
        _isDeleteMode = true;
        _selectedProp = null;
        ClearGhostInstance();
    }

    public void DisableDeleteMode()
    {
        _isDeleteMode = false;
        ClearGhostInstance();
        if (GridVisualizer.Instance != null) GridVisualizer.Instance.ClearPreview();
    }

    private void UpdateGhostPosition(Vector2Int cell, Vector3 rawHitPoint)
    {
        if (_ghostInstance == null) return;

        _ghostInstance.SetActive(true);
        Vector3 worldPos = ObstacleGrid.Instance.GetFootprintCenterWorld(cell, _selectedProp.footprintSize);
        worldPos.y = rawHitPoint.y + _pivotToBottomOffset;
        _ghostInstance.transform.position = worldPos;
    }

    private void ProcessPlacementInput()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            Vector2Int cell = ObstacleGrid.Instance.WorldToCell(hit.point);

            if (ObstacleGrid.Instance.CanPlaceProp(cell, _selectedProp.footprintSize))
            {
                Vector3 spawnPos = ObstacleGrid.Instance.GetFootprintCenterWorld(cell, _selectedProp.footprintSize);
                Quaternion targetRotation = Quaternion.Euler(0f, _currentRotationAngle, 0f);
                GameObject placed = Instantiate(_selectedProp.prefab, spawnPos, targetRotation);
                ObstacleGrid.Instance.RegisterProp(placed, cell, _selectedProp);
            }
        }
    }

    private void ProcessDeletionInput()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            ExecuteDeletionAtWorldPosition(hit.point);
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, ObstacleGrid.Instance.originWorldPos);
            if (groundPlane.Raycast(ray, out float enterDistance))
            {
                Vector3 intersectionPoint = ray.GetPoint(enterDistance);
                ExecuteDeletionAtWorldPosition(intersectionPoint);
            }
        }
    }

    private void ExecuteDeletionAtWorldPosition(Vector3 worldPoint)
    {
        bool deleted = ObstacleGrid.Instance.TryDeletePropAtWorldPos(worldPoint);
        if (deleted && GridVisualizer.Instance != null)
        {
            GridVisualizer.Instance.ClearPreview();
        }
    }

    private void ClearGhostInstance()
    {
        if (_ghostInstance != null)
        {
            Destroy(_ghostInstance);
            _ghostInstance = null;
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

    private void OnDestroy()
    {
        ClearGhostInstance();
    }
}