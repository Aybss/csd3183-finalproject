using UnityEngine;

public class GridInteractionManager : MonoBehaviour
{
    [Tooltip("The physics layer applied to your scene floor geometry.")]
    public LayerMask groundMask;

    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        // Right click down execution loop
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

            // Approach A: Try standard physics detection first
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            {
                ExecuteDeletionAtWorldPosition(hit.point);
            }
            // Approach B: Fallback calculation if your floor layout has no collider component
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