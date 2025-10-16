using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // [NEW INPUT]

[RequireComponent(typeof(Animator))]
public class SonicController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 25f;     // top speed while holding L
    public float accel = 10f;        // accelerate toward target speed
    public float decel = 12f;        // decelerate toward target speed
    public float turnSpeed = 180f;   // degrees/second

    [Header("Refs")]
    public CharacterController cc;   // optional

    private Animator anim;
    private float currentSpeed;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (!cc) cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Cache devices once (null-safe for platforms without kb/gamepad)
        var kb = Keyboard.current;    // [NEW INPUT]
        var gp = Gamepad.current;     // [NEW INPUT]

        // -------- Turning (A/D) --------
        float yaw = 0f;
        if (kb != null && kb.aKey.isPressed) yaw -= 1f;         // [NEW INPUT]
        if (kb != null && kb.dKey.isPressed) yaw += 1f;         // [NEW INPUT]
        transform.Rotate(0f, yaw * turnSpeed * Time.deltaTime, 0f);

        // Quick about-face when S is pressed
        if (kb != null && kb.sKey.wasPressedThisFrame)          // [NEW INPUT]
            transform.Rotate(0f, 180f, 0f);

        // -------- Intent (W/L) --------
        bool wHeld = kb != null && kb.wKey.isPressed;           // [NEW INPUT]
        bool lHeld = kb != null && kb.lKey.isPressed;           // [NEW INPUT]
        bool lDown = kb != null && kb.lKey.wasPressedThisFrame; // [NEW INPUT]

        // (Optional) allow gamepad left stick forward to count as "W"
        if (gp != null && gp.leftStick.up.ReadValue() > 0.5f) wHeld = true; // [NEW INPUT]

        // Fire the run intro exactly once when L is pressed while W is held
        if (lDown && wHeld)
            anim.SetTrigger("StartRun");

        // Choose target speed
        float target = 0f;
        if (wHeld) target = lHeld ? runSpeed : walkSpeed;

        // Smooth accel/decel
        float rate = (target > currentSpeed) ? accel : decel;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);

        // -------- Apply movement --------
        if (currentSpeed > 0.01f)
        {
            Vector3 v = transform.forward * currentSpeed;
            if (cc) cc.SimpleMove(v);
            else transform.position += v * Time.deltaTime;
        }

        // -------- Animator params --------
        anim.SetBool("IsWalking", wHeld);                 // mirrors W key
        anim.SetBool("IsRunning", wHeld && lHeld);        // W+L held
        anim.SetFloat("Speed01", Mathf.InverseLerp(0f, runSpeed, currentSpeed));

        // Optional extra action on Space (or gamepad South)
        bool spaceDown =
            (kb != null && kb.spaceKey.wasPressedThisFrame) ||
            (gp != null && gp.buttonSouth.wasPressedThisFrame); // [NEW INPUT]
        if (spaceDown)
            anim.SetTrigger("Play");
    }
}
