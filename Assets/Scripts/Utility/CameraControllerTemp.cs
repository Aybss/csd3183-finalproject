using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    public float movementSpeed = 20f;
    public float lookSensitivity = 3f;

    private float rotationX = 65f;
    private float rotationY = 45f;

    void Update()
    {
        // Only engage fly-look controls if holding right-click
        if (Input.GetMouseButton(1))
        {
            rotationX -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY += Input.GetAxis("Mouse X") * lookSensitivity;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            transform.rotation = Quaternion.Euler(rotationX, rotationY, 0);
        }

        // Basic WASD Movement directions relative to camera orientation
        float moveForward = Input.GetAxis("Vertical") * movementSpeed * Time.deltaTime;
        float moveStrafe = Input.GetAxis("Horizontal") * movementSpeed * Time.deltaTime;

        float moveUp = 0f;
        if (Input.GetKey(KeyCode.E)) moveUp = movementSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) moveUp = -movementSpeed * Time.deltaTime;

        transform.Translate(new Vector3(moveStrafe, moveUp, moveForward));
    }
}