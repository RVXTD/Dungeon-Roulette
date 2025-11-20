using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float accel = 10f;
    public float decel = 12f;
    public float jumpForce = 8f;
    public float gravity = 20f;

    [Header("Look Settings")]
    public float mouseSensitivity = 100f;
    public float controllerSensitivity = 150f;
    public float clampAngle = 85f;

    [Header("References")]
    public CharacterController cc;
    public Transform cameraHolder; // Drag your Main Camera here in Inspector

    private float currentSpeed;
    private Vector3 moveDir;
    private float verticalVelocity;
    private float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var mouse = Mouse.current;

        // ======== LOOK AROUND ========
        float mouseX = 0f;
        float mouseY = 0f;

        if (mouse != null)
        {
            mouseX = mouse.delta.x.ReadValue() * mouseSensitivity * Time.deltaTime;
            mouseY = mouse.delta.y.ReadValue() * mouseSensitivity * Time.deltaTime;
        }

        if (gp != null)
        {
            mouseX += gp.rightStick.x.ReadValue() * controllerSensitivity * Time.deltaTime;
            mouseY += gp.rightStick.y.ReadValue() * controllerSensitivity * Time.deltaTime;
        }

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -clampAngle, clampAngle);

        // Apply camera pitch
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotate player body (yaw)
        transform.Rotate(Vector3.up * mouseX);


        // ======== MOVEMENT ========
        float moveX = 0f;
        float moveZ = 0f;

        if (kb != null)
        {
            if (kb.wKey.isPressed) moveZ += 1f;
            if (kb.sKey.isPressed) moveZ -= 1f;
            if (kb.aKey.isPressed) moveX -= 1f;
            if (kb.dKey.isPressed) moveX += 1f;
        }

        if (gp != null)
        {
            moveX = gp.leftStick.x.ReadValue();
            moveZ = gp.leftStick.y.ReadValue();
        }

        Vector3 inputDir = new Vector3(moveX, 0f, moveZ).normalized;

        bool lHeld = kb != null && kb.lKey.isPressed;
        float target = inputDir.magnitude > 0f ? (lHeld ? runSpeed : walkSpeed) : 0f;

        float rate = (target > currentSpeed) ? accel : decel;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);

        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.z;
        move *= currentSpeed;

        // ======== JUMP + GRAVITY ========
        bool isGrounded = cc ? cc.isGrounded : transform.position.y <= 0.01f;
        bool jumpPressed = (kb != null && kb.spaceKey.wasPressedThisFrame) ||
                           (gp != null && gp.buttonSouth.wasPressedThisFrame);

        if (isGrounded)
        {
            verticalVelocity = -1f;
            if (jumpPressed)
                verticalVelocity = jumpForce;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        moveDir = new Vector3(move.x, verticalVelocity, move.z);

        if (cc)
        {
            cc.Move(moveDir * Time.deltaTime);
        }
        else
        {
            transform.position += moveDir * Time.deltaTime;
            if (transform.position.y < 0f)
                transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        }
    }
}
