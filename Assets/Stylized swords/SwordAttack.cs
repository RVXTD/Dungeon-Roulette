using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------
// LEFT-CLICK melee: spherecast from CAMERA during the swing
// - Much more forgiving than a sword trigger collider
// - Damages each target once per swing (tracked with HashSet)
// ---------------------------------------------------------
public class SwordAttack : MonoBehaviour
{
    [Header("Attack Timing")]
    public float swingTime = 0.20f;       // how long we sample hits
    public float cooldownTime = 0.40f;    // delay before next swing

    [Header("Hit Settings")]
    public float damage = 25f;            // damage per target
    public float range = 2.2f;            // how far from camera
    public float radius = 0.45f;          // “forgiveness” width of the sweep
    public LayerMask hitMask = ~0;        // layers we can hit (set in Inspector)

    [Header("Refs")]
    public Camera cam;                    // assign your Main Camera here

    bool isSwinging;
    bool onCooldown;
    HashSet<Object> hitThisSwing = new HashSet<Object>(); // tracks per-swing hits

    void Reset()
    {
        // Try to auto-find the main camera if not assigned
        if (!cam && Camera.main) cam = Camera.main;
    }

    void Update()
    {
        // Left-click to start a swing
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            !isSwinging && !onCooldown)
        {
            StartCoroutine(Swing());
        }
    }

    IEnumerator Swing()
    {
        if (!cam) { Debug.LogWarning("SwordAttack: No Camera assigned."); yield break; }

        isSwinging = true;
        onCooldown = true;
        hitThisSwing.Clear();

        float t = 0f;
        while (t < swingTime)
        {
            t += Time.deltaTime;

            // ---- SphereCast forward from the camera ----
            Vector3 origin = cam.transform.position;
            Vector3 dir = cam.transform.forward;

            // We use SphereCastAll so very close targets still register
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, dir, range, hitMask, QueryTriggerInteraction.Ignore);

            foreach (var h in hits)
            {
                // Only damage objects that implement IDamageable
                if (h.collider && h.collider.TryGetComponent<IDamageable>(out var dmg))
                {
                    // Make sure each target is damaged only once per swing
                    if (!hitThisSwing.Contains(dmg as Object))
                    {
                        dmg.TakeDamage(damage);
                        hitThisSwing.Add(dmg as Object);
                        // Optional: Debug ray
                        // Debug.DrawLine(origin, h.point, Color.red, 0.1f);
                    }
                }
            }

            yield return null; // keep sampling every frame during the swing
        }

        // Cooldown
        yield return new WaitForSeconds(cooldownTime);
        isSwinging = false;
        onCooldown = false;
    }
}