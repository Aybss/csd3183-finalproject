using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    public float movementSpeed = 20f;
    public float lookSensitivity = 3f;

    [Header("Agent Lock-On (driven by AgentOverlayUI's row buttons)")]
    public float followDistance = 8f;
    public float followHeight = 4f;
    public float followLerpSpeed = 5f;

    [Header("Map Alignment (driven by the simulation UI's toggle)")]
    public float topDownHeight = 45f;

    private float rotationX = 65f;
    private float rotationY = 45f;
    private Transform lockedTarget;
    private bool topDownAligned;
    private Vector3 topDownCenter;

    public bool IsLocked => lockedTarget != null;
    public bool IsTopDownAligned => topDownAligned;

    public void SetLockedTarget(Transform target)
    {
        lockedTarget = target;
        topDownAligned = false; // the two view modes are mutually exclusive
    }

    public void ClearLock()
    {
        lockedTarget = null;
    }

    // mapCenter should be the world-space center of the terrain (width/2,
    // *, height/2 in grid units, scaled by cellSize).
    public void SetTopDownAligned(bool enabled, Vector3 mapCenter)
    {
        topDownAligned = enabled;
        topDownCenter = mapCenter;
        if (enabled) lockedTarget = null;
    }

    void Update()
    {
        // Right-click still free-looks even while locked, so you can orbit
        // around the selected agent instead of being stuck at one angle.
        if (Input.GetMouseButton(1) && !topDownAligned)
        {
            rotationX -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY += Input.GetAxis("Mouse X") * lookSensitivity;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            transform.rotation = Quaternion.Euler(rotationX, rotationY, 0);
        }

        if (topDownAligned)
        {
            AlignTopDown();
            return;
        }

        if (lockedTarget != null)
        {
            FollowLockedTarget();
            return; // position is target-driven while locked — ignore WASD/E/Q
        }

        // Basic WASD Movement directions relative to camera orientation
        float moveForward = Input.GetAxis("Vertical") * movementSpeed * Time.deltaTime;
        float moveStrafe = Input.GetAxis("Horizontal") * movementSpeed * Time.deltaTime;

        float moveUp = 0f;
        if (Input.GetKey(KeyCode.E)) moveUp = movementSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) moveUp = -movementSpeed * Time.deltaTime;

        transform.Translate(new Vector3(moveStrafe, moveUp, moveForward));
    }

    private void FollowLockedTarget()
    {
        if (lockedTarget == null) return;

        Vector3 desiredPosition = lockedTarget.position - transform.forward * followDistance + Vector3.up * followHeight;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerpSpeed * Time.deltaTime);
    }

    private void AlignTopDown()
    {
        Vector3 desiredPosition = topDownCenter + Vector3.up * topDownHeight;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerpSpeed * Time.deltaTime);

        Quaternion desiredRotation = Quaternion.Euler(90f, 0f, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, followLerpSpeed * Time.deltaTime);
    }
}
