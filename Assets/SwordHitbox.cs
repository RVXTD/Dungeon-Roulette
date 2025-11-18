using System.Collections.Generic;
using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 25f;                    // How much damage per hit
    public string[] damageableLayers = { "Default", "Enemy" }; // Layers that can be hit
    public bool active;                           // Whether hitbox is currently active

    int layerMask;                                // Internal layer mask used for filtering

    // to avoid hitting the same collider multiple times in 1 swing
    private readonly HashSet<Collider> hitThisSwing = new HashSet<Collider>();

    void Awake()
    {
        // Combine allowed layers into one mask
        foreach (var ln in damageableLayers)
            layerMask |= 1 << LayerMask.NameToLayer(ln);
    }

    void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    void OnTriggerStay(Collider other)
    {
        // this handles the "enemy is already inside while running at me" case
        TryHit(other);
    }

    void TryHit(Collider other)
    {
        // Ignore if not swinging
        if (!active) return;

        // Ignore objects on disallowed layers
        if (((1 << other.gameObject.layer) & layerMask) == 0) return;

        // Don't hit same target multiple times this swing
        if (hitThisSwing.Contains(other)) return;

        // If object can take damage, apply it
        if (other.TryGetComponent<IDamageable>(out var dmg))
        {
            dmg.TakeDamage(damage);
            // Debug.Log($"{other.name} took {damage} damage.");
        }

        hitThisSwing.Add(other);
    }

    // These are helper methods you can call from your attack script / animation
    public void Enable()
    {
        active = true;
        hitThisSwing.Clear();   // new swing -> can hit everything again
    }

    public void Disable()
    {
        active = false;
        hitThisSwing.Clear();
    }
}