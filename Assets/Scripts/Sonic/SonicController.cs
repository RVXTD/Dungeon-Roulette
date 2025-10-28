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
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        // -------- Turning (A/D) --------
        float yaw = 0f;
        if (kb != null && kb.aKey.isPressed) yaw -= 1f;

        // D only turns if W isn't held (so it can double as boost)
        bool wHeld = kb != null && kb.wKey.isPressed;
        bool dHeld = kb != null && kb.dKey.isPressed;
        if (!wHeld && dHeld) yaw += 1f;
        transform.Rotate(0f, yaw * turnSpeed * Time.deltaTime, 0f);

        if (kb != null && kb.sKey.wasPressedThisFrame)
            transform.Rotate(0f, 180f, 0f);

        // -------- Intent (W + D = Boost) --------
        bool boostHeld = wHeld && dHeld;
        bool boostDown = wHeld && kb != null && kb.dKey.wasPressedThisFrame;

        // Fire run intro when boost starts
        if (boostDown)
            anim.SetTrigger("StartRun");

        // Choose target speed
        float target = 0f;
        if (wHeld) target = boostHeld ? runSpeed : walkSpeed;

        // Smooth accel/decel
        float rate = (target > currentSpeed) ? accel : decel;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);

        // Apply movement
        if (currentSpeed > 0.01f)
        {
            Vector3 v = transform.forward * currentSpeed;
            if (cc) cc.SimpleMove(v);
            else transform.position += v * Time.deltaTime;
        }

        // Animator params
        anim.SetBool("IsWalking", wHeld);
        anim.SetBool("IsRunning", boostHeld);
        anim.SetFloat("Speed01", Mathf.InverseLerp(0f, runSpeed, currentSpeed));

        bool spaceDown = (kb != null && kb.spaceKey.wasPressedThisFrame) ||
                         (gp != null && gp.buttonSouth.wasPressedThisFrame);
        if (spaceDown)
            anim.SetTrigger("Play");
    }
}