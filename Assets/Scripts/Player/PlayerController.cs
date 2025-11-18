using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 9f;
    public float gravity = -9.81f;
    public float jumpForce = 5f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;

    // ---------------------------------------------
    // PLAYER ABILITY SCRIPT (DASH)
    // Implements dash movement activated by F key.
    // ---------------------------------------------
    [Header("Dash Ability")]
    public KeyCode dashKey = KeyCode.F;
    public float dashSpeed = 18f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 1.0f;
    public bool allowAirDash = true;

    private CharacterController cc;
    private float vertVelocity;
    private float yaw;
    private bool isDashing = false;
    private bool hasAirDashed = false;
    private Vector3 lastPlanarMove = Vector3.zero;
    private float nextDashTime = 0f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 camForward = cameraTransform ? cameraTransform.forward : transform.forward;
        Vector3 camRight = cameraTransform ? cameraTransform.right : transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        if (moveDir.sqrMagnitude > 0.0001f)
            lastPlanarMove = moveDir;

        bool isPressingOnlyW = v > 0 && h == 0 && Input.GetKey(KeyCode.W);
        float currentSpeed = isPressingOnlyW && Input.GetKey(KeyCode.LeftShift)
            ? sprintSpeed
            : moveSpeed;

        // -------------------------------------
        // PLAYER ABILITY SCRIPT (DASH)
        // Dash input and cooldown logic.
        // -------------------------------------
        TryStartDash(moveDir);

        if (cc.isGrounded)
        {
            hasAirDashed = false;
            if (!isDashing)
                vertVelocity = -1f;

            if (!isDashing && Input.GetKeyDown(KeyCode.Space))
                vertVelocity = jumpForce;
        }
        else
        {
            if (!isDashing)
                vertVelocity += gravity * Time.deltaTime;
        }

        if (!isDashing)
        {
            Vector3 velocity = moveDir * currentSpeed;
            velocity.y = vertVelocity;
            cc.Move(velocity * Time.deltaTime);
        }
    }

    // -------------------------------------
    // PLAYER ABILITY SCRIPT (DASH)
    // Checks dash input, direction, and air dash limits.
    // -------------------------------------
    private void TryStartDash(Vector3 moveDir)
    {
        if (isDashing || Time.time < nextDashTime) return;
        if (!Input.GetKeyDown(dashKey)) return;

        if (!cc.isGrounded)
        {
            if (!allowAirDash || hasAirDashed) return;
            hasAirDashed = true;
        }

        Vector3 dashDir = moveDir.sqrMagnitude > 0.001f
            ? moveDir
            : (lastPlanarMove.sqrMagnitude > 0.001f ? lastPlanarMove : transform.forward);

        StartCoroutine(DashRoutine(dashDir));
    }

    // -------------------------------------
    // PLAYER ABILITY SCRIPT (DASH)
    // Moves player quickly in a set direction.
    // -------------------------------------
    private System.Collections.IEnumerator DashRoutine(Vector3 dashDir)
    {
        isDashing = true;
        float savedVert = vertVelocity;
        vertVelocity = 0f;

        float endTime = Time.time + dashDuration;

        while (Time.time < endTime)
        {
            Vector3 dashVelocity = dashDir * dashSpeed;
            if (cc.isGrounded) dashVelocity.y = -2f;
            cc.Move(dashVelocity * Time.deltaTime);
            yield return null;
        }

        isDashing = false;
        nextDashTime = Time.time + dashCooldown;

        if (!cc.isGrounded)
            vertVelocity = savedVert;
        else
            vertVelocity = -1f;
    }
}