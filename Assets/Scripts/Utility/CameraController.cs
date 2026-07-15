using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float panSpeed = 15f;
    public float keyboardPanSpeed = 15f;

    [Header("Zoom")]
    public float zoomSpeed = 10f;
    public float minHeight = 2f;
    public float maxHeight = 40f;

    private bool _isPanModeActive = false;
    private Vector3 _dragOrigin;

    private void Update()
    {
        // 1. Zoom Logic (Always active via scroll wheel)
        HandleZoom();

        // 2. Keyboard Panning (Always active via WASD/Arrows)
        HandleKeyboardPan();

        // 3. Mouse Drag Panning (Active only if UI Pan Mode button is toggled ON)
        if (_isPanModeActive)
        {
            HandleMouseDragPan();
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Move camera along its forward vector for zooming
            Vector3 zoomDir = transform.forward * scroll * zoomSpeed * 10f;
            Vector3 targetPos = transform.position + zoomDir * Time.deltaTime;

            // Clamp camera height to prevent plunging below ground or escaping layout
            targetPos.y = Mathf.Clamp(targetPos.y, minHeight, maxHeight);
            transform.position = targetPos;
        }
    }

    private void HandleKeyboardPan()
    {
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right Arrow
        float vertical = Input.GetAxisRaw("Vertical");     // W/S or Up/Down Arrow

        if (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f)
        {
            // Flatten directions so looking down doesn't skew moving forward
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;
            transform.position += moveDirection * keyboardPanSpeed * Time.deltaTime;
        }
    }

    private void HandleMouseDragPan()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Check flat floor math coordinates when starting the drag loop
            _dragOrigin = Input.mousePosition;
            return;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 difference = _dragOrigin - currentMousePos;
            _dragOrigin = currentMousePos;

            // Map structural mouse displacement vector directly to world positions
            Vector3 moveDir = new Vector3(difference.x, 0f, difference.y);

            // Adjust translation matching your viewing angle rotation matrix transformations
            Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = transform.right; right.y = 0f; right.Normalize();
            Vector3 worldMove = (forward * moveDir.z + right * moveDir.x) * (panSpeed * 0.01f);

            transform.position += worldMove;
        }
    }

    public void TogglePanMode(bool isActive)
    {
        _isPanModeActive = isActive;
        Debug.Log($"[Camera] Pan Mode Status: {_isPanModeActive}");
    }
}