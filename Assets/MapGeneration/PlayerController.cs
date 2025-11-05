using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 9f;
    public float gravity = -9.81f;
    public float jumpForce = 5f;
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;

    private CharacterController cc;
    private float vertVelocity;
    private float yaw;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // --- Mouse Look ---
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // --- Input ---
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // --- Camera-relative movement ---
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        // --- Sprint (only when W and nothing else) ---
        bool isPressingOnlyW = v > 0 && h == 0 && Input.GetKey(KeyCode.W);
        float currentSpeed = isPressingOnlyW && Input.GetKey(KeyCode.LeftShift)
            ? sprintSpeed
            : moveSpeed;

        // --- Grounded / Jump / Gravity ---
        if (cc.isGrounded)
        {
            // keep us stuck to ground
            vertVelocity = -1f;

            // jump
            if (Input.GetKeyDown(KeyCode.Space))
            {
                vertVelocity = jumpForce;
            }
        }
        else
        {
            // falling
            vertVelocity += gravity * Time.deltaTime;
        }

        // --- Final movement ---
        Vector3 velocity = moveDir * currentSpeed;
        velocity.y = vertVelocity;

        cc.Move(velocity * Time.deltaTime);
    }
}