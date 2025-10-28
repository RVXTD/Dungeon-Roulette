using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float mouseSensitivity = 2f;

    CharacterController cc;
    float vertVelocity;
    float yaw;

    void Awake() { cc = GetComponent<CharacterController>(); Cursor.lockState = CursorLockMode.Locked; }

    void Update()
    {
        // Mouse look (yaw only for simplicity)
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // WASD movement on XZ plane
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 input = new Vector3(h, 0, v);
        Vector3 world = transform.TransformDirection(input).normalized;

        // Gravity
        if (cc.isGrounded) vertVelocity = -1f; else vertVelocity += gravity * Time.deltaTime;

        Vector3 move = world * moveSpeed + Vector3.up * vertVelocity;
        cc.Move(move * Time.deltaTime);
    }
}
