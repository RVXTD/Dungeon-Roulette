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

    [Header("Ability Key")]
    public KeyCode abilityKey = KeyCode.F;

    // -------------------------
    // ABILITIES (4 total)
    // -------------------------
    public enum AbilityType { Dash, Freeze, Thorns, Invincibility }

    [Header("Current Ability (RoundManager sets this)")]
    public AbilityType currentAbility = AbilityType.Dash;

    [Header("Dash Ability")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 1.0f;
    public bool allowAirDash = true;

    [Header("Freeze Ability")]
    public float freezeRadius = 10f;
    public float freezeDuration = 3f;
    public float freezeCooldown = 10f;

    [Header("Thorns Ability")]
    public float thornsDuration = 8f;
    public float thornsCooldown = 15f;

    [Header("Invincibility Ability")]
    public float invincibleDuration = 5f;
    public float invincibleCooldown = 20f;

    [Header("Freeze Targeting")]
    public LayerMask enemyLayerMask;

    private CharacterController cc;
    private PlayerHealth playerHealth;
    private PlayerStamina playerStamina;

    private float vertVelocity;
    private float yaw;

    // dash runtime
    private bool isDashing = false;
    private bool hasAirDashed = false;
    private Vector3 lastPlanarMove = Vector3.zero;

    // ability meter state
    private bool abilityRunning = false;
    private bool abilityOnCooldown = false;
    private float abilityDurationTimer = 0f;
    private float abilityCooldownTimer = 0f;
    private float activeAbilityDuration = 0f;
    private float activeAbilityCooldown = 0f;

    // -------------------------
    // PUBLIC UI READ-ONLY
    // -------------------------
    public string CurrentAbilityName => currentAbility switch
    {
        AbilityType.Dash => "DASH",
        AbilityType.Freeze => "FREEZE",
        AbilityType.Thorns => "THORNS",
        AbilityType.Invincibility => "INVINCIBLE",
        _ => "ABILITY"
    };

    // 1 = ready, 1->0 while active, then 0->1 while cooling down
    public float AbilityBar01
    {
        get
        {
            if (abilityRunning && activeAbilityDuration > 0.0001f)
                return Mathf.Clamp01(1f - (abilityDurationTimer / activeAbilityDuration));

            if (abilityOnCooldown && activeAbilityCooldown > 0.0001f)
                return Mathf.Clamp01(abilityCooldownTimer / activeAbilityCooldown);

            return 1f;
        }
    }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerStamina = GetComponent<PlayerStamina>();

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

        // --- Sprint (stamina gated) ---
        bool wantsSprint = Input.GetKey(KeyCode.LeftShift);
        bool isMoving = moveDir.sqrMagnitude > 0.001f;

        bool canSprintNow =
            wantsSprint &&
            isMoving &&
            !isDashing &&
            (playerStamina == null || playerStamina.CanSprint);

        float currentSpeed = canSprintNow ? sprintSpeed : moveSpeed;

        if (canSprintNow && playerStamina != null)
            playerStamina.DrainWhileSprinting();

        // ✅ Ability activation (F)
        if (Input.GetKeyDown(abilityKey))
        {
            TryUseAbility(moveDir);
        }

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

        // --- Movement when not dashing ---
        if (!isDashing)
        {
            Vector3 velocity = moveDir * currentSpeed;
            velocity.y = vertVelocity;
            cc.Move(velocity * Time.deltaTime);
        }
    }

    // Called by RoundManager when a new round starts
    public void SetAbility(AbilityType newAbility)
    {
        currentAbility = newAbility;

        // clear any leftover buffs
        if (playerHealth != null)
        {
            playerHealth.thornsActive = false;
            playerHealth.isInvincible = false;
        }

        // reset bar to full (ready)
        abilityRunning = false;
        abilityOnCooldown = false;
        abilityDurationTimer = 0f;
        abilityCooldownTimer = 0f;
        activeAbilityDuration = 0f;
        activeAbilityCooldown = 0f;
    }

    private void TryUseAbility(Vector3 moveDir)
    {
        if (abilityRunning || abilityOnCooldown) return; // only one at a time
        StartCoroutine(AbilityLifecycle(moveDir));
    }

    private IEnumerator AbilityLifecycle(Vector3 moveDir)
    {
        abilityRunning = true;

        activeAbilityDuration = GetAbilityDuration(currentAbility);
        activeAbilityCooldown = GetAbilityCooldown(currentAbility);

        abilityDurationTimer = 0f;
        abilityCooldownTimer = 0f;

        // --- START ability effect ---
        switch (currentAbility)
        {
            case AbilityType.Dash:
                yield return StartCoroutine(DashRoutine(moveDir));
                // DashRoutine already lasts dashDuration, so duration bar will drain during it.
                break;

            case AbilityType.Freeze:
                ActivateFreeze();
                // freezeDuration is “effect duration”, we still use it for the bar timing
                break;

            case AbilityType.Thorns:
                if (playerHealth != null) playerHealth.thornsActive = true;
                break;

            case AbilityType.Invincibility:
                if (playerHealth != null) playerHealth.isInvincible = true;
                break;
        }

        // --- DURATION phase (drain bar) ---
        while (abilityDurationTimer < activeAbilityDuration)
        {
            abilityDurationTimer += Time.deltaTime;
            yield return null;
        }

        // --- END buffs if needed ---
        switch (currentAbility)
        {
            case AbilityType.Thorns:
                if (playerHealth != null) playerHealth.thornsActive = false;
                break;

            case AbilityType.Invincibility:
                if (playerHealth != null) playerHealth.isInvincible = false;
                break;
        }

        abilityRunning = false;

        // --- COOLDOWN phase (fill bar) ---
        abilityOnCooldown = true;
        abilityCooldownTimer = 0f;

        while (abilityCooldownTimer < activeAbilityCooldown)
        {
            abilityCooldownTimer += Time.deltaTime;
            yield return null;
        }

        abilityOnCooldown = false;
        activeAbilityDuration = 0f;
        activeAbilityCooldown = 0f;
    }

    private float GetAbilityDuration(AbilityType a)
    {
        return a switch
        {
            AbilityType.Dash => dashDuration,
            AbilityType.Freeze => freezeDuration,
            AbilityType.Thorns => thornsDuration,
            AbilityType.Invincibility => invincibleDuration,
            _ => 0f
        };
    }

    private float GetAbilityCooldown(AbilityType a)
    {
        return a switch
        {
            AbilityType.Dash => dashCooldown,
            AbilityType.Freeze => freezeCooldown,
            AbilityType.Thorns => thornsCooldown,
            AbilityType.Invincibility => invincibleCooldown,
            _ => 1f
        };
    }

    // ------------------ DASH ------------------
    private IEnumerator DashRoutine(Vector3 moveDir)
    {
        if (isDashing) yield break;

        if (!cc.isGrounded)
        {
            if (!allowAirDash || hasAirDashed) yield break;
            hasAirDashed = true;
        }

        Vector3 dashDir = moveDir.sqrMagnitude > 0.001f
            ? moveDir
            : (lastPlanarMove.sqrMagnitude > 0.001f ? lastPlanarMove : transform.forward);

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

        if (!cc.isGrounded)
            vertVelocity = savedVert;
        else
            vertVelocity = -1f;
    }

    // ------------------ FREEZE ------------------
    private void ActivateFreeze()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, freezeRadius);

        foreach (var hit in hits)
        {
            EnemyScript enemy = hit.GetComponentInParent<EnemyScript>();
            if (enemy != null)
                enemy.FreezeForDuration(freezeDuration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, freezeRadius);
    }
}
