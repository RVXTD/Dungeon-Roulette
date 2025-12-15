using UnityEngine;
using System.Collections;

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
    // ---------------------------------------------
    [Header("Dash Ability")]
    public KeyCode dashKey = KeyCode.F;
    public float dashSpeed = 18f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 1.0f;
    public bool allowAirDash = true;

    // ---------------------------------------------
    // NEW ABILITIES
    // ---------------------------------------------
    [Header("Freeze Ability")]
    public KeyCode freezeKey = KeyCode.Z;   // Freeze = Z
    public float freezeRadius = 10f;
    public float freezeDuration = 3f;
    public LayerMask enemyLayerMask;

    [Header("Thorns Ability")]
    public KeyCode thornsKey = KeyCode.X;   // Thorns = X
    public float thornsDuration = 8f;
    public float thornsCooldown = 15f;

    [Header("Invincibility Ability")]
    public KeyCode invincibleKey = KeyCode.C;  // Invincibility = C
    public float invincibleDuration = 5f;
    public float invincibleCooldown = 20f;

    private CharacterController cc;
    private PlayerHealth playerHealth;

    private float vertVelocity;
    private float yaw;
    private bool isDashing = false;
    private bool hasAirDashed = false;
    private Vector3 lastPlanarMove = Vector3.zero;
    private float nextDashTime = 0f;

    private bool thornsOnCooldown = false;
    private bool invincibleOnCooldown = false;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // --- Mouse Look ---
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // --- Movement Input ---
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

        // --- Dash ---
        TryStartDash(moveDir);

        // --- Gravity / Jump ---
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

        // --- Normal movement when not dashing ---
        if (!isDashing)
        {
            Vector3 velocity = moveDir * currentSpeed;
            velocity.y = vertVelocity;
            cc.Move(velocity * Time.deltaTime);
        }

        // --- Ability inputs (freeze, thorns, invincibility) ---
        HandleAbilities();
    }

    // -------------------------------------
    // DASH
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

    private IEnumerator DashRoutine(Vector3 dashDir)
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

    // -------------------------------------
    // NEW ABILITIES
    // -------------------------------------
    private void HandleAbilities()
    {
        if (Input.GetKeyDown(freezeKey))
        {
            ActivateFreeze();
        }

        if (Input.GetKeyDown(thornsKey))
        {
            if (!thornsOnCooldown && playerHealth != null)
                StartCoroutine(ThornsRoutine());
        }

        if (Input.GetKeyDown(invincibleKey))
        {
            if (!invincibleOnCooldown && playerHealth != null)
                StartCoroutine(InvincibilityRoutine());
        }
    }

    // Freeze all enemies in a radius
    private void ActivateFreeze()
    {
        // Grab everything in range; we’ll filter by EnemyScript
        Collider[] hits = Physics.OverlapSphere(transform.position, freezeRadius);

        foreach (var hit in hits)
        {
            EnemyScript enemy = hit.GetComponentInParent<EnemyScript>();
            if (enemy != null)
            {
                enemy.FreezeForDuration(freezeDuration);
            }
        }
    }

    // Thorns buff: reflect damage while active
    private IEnumerator ThornsRoutine()
    {
        thornsOnCooldown = true;
        playerHealth.thornsActive = true;

        yield return new WaitForSeconds(thornsDuration);

        playerHealth.thornsActive = false;

        yield return new WaitForSeconds(thornsCooldown);
        thornsOnCooldown = false;
    }

    // Invincibility buff: ignore all damage while active
    private IEnumerator InvincibilityRoutine()
    {
        invincibleOnCooldown = true;
        playerHealth.isInvincible = true;

        yield return new WaitForSeconds(invincibleDuration);

        playerHealth.isInvincible = false;

        yield return new WaitForSeconds(invincibleCooldown);
        invincibleOnCooldown = false;
    }
    private void OnDrawGizmosSelected()
    {
        // Only run in editor when object is selected
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, freezeRadius);
    }
}
