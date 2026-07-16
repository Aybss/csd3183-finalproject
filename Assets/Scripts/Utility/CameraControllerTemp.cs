using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    public float movementSpeed = 20f;
    public float lookSensitivity = 3f;

    [Header("Agent Lock-On (driven by AgentOverlayUI's row buttons)")]
    public float followDistance = 8f;
    public float followHeight = 4f;
    public float followLerpSpeed = 5f;

    private float rotationX = 65f;
    private float rotationY = 45f;
    private Transform lockedTarget;

    public bool IsLocked => lockedTarget != null;

    public void SetLockedTarget(Transform target)
    {
        lockedTarget = target;
    }

    public void ClearLock()
    {
        lockedTarget = null;
    }

    void Update()
    {
        // Right-click still free-looks even while locked, so you can orbit
        // around the selected agent instead of being stuck at one angle.
        if (Input.GetMouseButton(1))
        {
            rotationX -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY += Input.GetAxis("Mouse X") * lookSensitivity;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            transform.rotation = Quaternion.Euler(rotationX, rotationY, 0);
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
}
