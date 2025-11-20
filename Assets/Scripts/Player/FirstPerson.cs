using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFirstPerson : MonoBehaviour
{
    public Transform target;
    public Vector3 headOffset = new Vector3(0f, 1.6f, 0.15f);
    public float sensitivity = 120f; // deg/sec per mouse unit
    public bool lockCursor = true;

    float pitch, yaw;

    void OnEnable()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        Vector2 delta = Vector2.zero;
        if (Mouse.current != null) delta += Mouse.current.delta.ReadValue();
        if (Gamepad.current != null) delta += Gamepad.current.rightStick.ReadValue() * 10f; // optional

        yaw += delta.x * sensitivity * Time.deltaTime;
        pitch -= delta.y * sensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target.TransformPoint(headOffset);

        target.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}