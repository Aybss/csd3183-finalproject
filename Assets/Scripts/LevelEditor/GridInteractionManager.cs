using UnityEngine;

public class GridInteractionManager : MonoBehaviour
{
    [Tooltip("The physics layer applied to your scene floor geometry.")]
    public LayerMask groundMask;

    private Camera _cam;
    private bool _isDeleteMode = false;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        if (_isDeleteMode)
        {
            HandleDeleteHoverVisual();
        }

        if (Input.GetMouseButtonDown(1))
        {
            ProcessDeletionInput();
        }
        else if (Input.GetMouseButtonDown(0) && _isDeleteMode)
        {
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                ProcessDeletionInput();
            }
        }
    }

    private void HandleDeleteHoverVisual()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask) && GridVisualizer.Instance != null)
        {
            Vector2Int hoveredCell = ObstacleGrid.Instance.WorldToCell(hit.point);
            GridVisualizer.Instance.UpdatePreview(hoveredCell, Vector2Int.one, false);
        }
        else if (GridVisualizer.Instance != null)
        {
            GridVisualizer.Instance.ClearPreview();
        }
    }

    public void EnableDeleteMode()
    {
        _isDeleteMode = true;
        Debug.Log("[Interaction] Delete Mode Enabled via UI.");
    }

    public void DisableDeleteMode()
    {
        _isDeleteMode = false;
        if (GridVisualizer.Instance != null) GridVisualizer.Instance.ClearPreview();
        Debug.Log("[Interaction] Delete Mode Disabled.");
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
        if (deleted)
        {
            Debug.Log($"[Interaction] Successfully cleared asset from position: {worldPoint}");
        }
    }
}