using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwordAttack : MonoBehaviour
{
    [Header("Attack Timing")]
    public float swingTime = 0.18f;
    public float returnTime = 0.10f;
    public float cooldownTime = 0.20f;

    [Header("Swing Direction & Offset")]
    [Tooltip("How much to rotate (in degrees) on each axis when swinging.")]
    public Vector3 swingRotation = new Vector3(55f, 0f, 0f);
    [Tooltip("How far to move (in meters) on each axis when swinging.")]
    public Vector3 swingOffset = new Vector3(0.04f, -0.04f, 0.22f);

    [Header("Hit Settings")]
    public float damage = 25f;
    public float range = 2.2f;
    public float radius = 0.45f;
    public LayerMask hitMask = ~0;

    [Header("Refs")]
    public Camera cam;
    public Transform ownerRoot;
    public Transform swingTransform;

    private bool isSwinging;
    private bool onCooldown;
    private readonly HashSet<Object> hitThisSwing = new();

    void Reset()
    {
        if (!cam && Camera.main) cam = Camera.main;
        if (!ownerRoot) ownerRoot = transform.root;
        if (!swingTransform) swingTransform = transform;
    }

    void Update()
    {
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            !isSwinging && !onCooldown)
        {
            StartCoroutine(Swing());
        }
    }

    IEnumerator Swing()
    {
        if (!cam || !swingTransform) yield break;

        isSwinging = true;
        onCooldown = true;
        hitThisSwing.Clear();

        Quaternion startRot = swingTransform.localRotation;
        Vector3 startPos = swingTransform.localPosition;

        // Use your inspector values here
        Quaternion hitRot = startRot * Quaternion.Euler(swingRotation);
        Vector3 hitPos = startPos + swingOffset;

        // --- Swing Down ---
        float t = 0f;
        while (t < swingTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / swingTime);
            float e = 1f - Mathf.Cos(k * Mathf.PI * 0.5f); // ease out

            swingTransform.localRotation = Quaternion.Slerp(startRot, hitRot, e);
            swingTransform.localPosition = Vector3.Lerp(startPos, hitPos, e);

            DoHitScan();
            yield return null;
        }

        // --- Return Up ---
        t = 0f;
        while (t < returnTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / returnTime);
            float e = Mathf.Sin(k * Mathf.PI * 0.5f); // ease in

            swingTransform.localRotation = Quaternion.Slerp(hitRot, startRot, e);
            swingTransform.localPosition = Vector3.Lerp(hitPos, startPos, e);
            yield return null;
        }

        swingTransform.localRotation = startRot;
        swingTransform.localPosition = startPos;

        yield return new WaitForSeconds(cooldownTime);
        isSwinging = false;
        onCooldown = false;
    }

    void DoHitScan()
    {
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;

        var hits = Physics.SphereCastAll(origin, radius, dir, range, hitMask, QueryTriggerInteraction.Ignore);

        foreach (var h in hits)
        {
            if (!h.collider) continue;
            if (IsSelf(h.collider)) continue;

            if (h.collider.TryGetComponent<IDamageable>(out var dmg))
            {
                var key = dmg as Object;
                if (!hitThisSwing.Contains(key))
                {
                    dmg.TakeDamage(damage);
                    hitThisSwing.Add(key);
                }
            }
        }
    }

    bool IsSelf(Collider col)
    {
        if (!ownerRoot) return false;
        Transform t = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
        return t == ownerRoot || t.IsChildOf(ownerRoot);
    }
}
