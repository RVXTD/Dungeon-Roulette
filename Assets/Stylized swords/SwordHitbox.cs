using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 25f;                    // How much damage per hit
    public string[] damageableLayers = { "Default", "Enemy" }; // Layers that can be hit
    public bool active;                           // Whether hitbox is currently active

    int layerMask;                                // Internal layer mask used for filtering

    void Awake()
    {
        // Combine allowed layers into one mask
        foreach (var ln in damageableLayers)
            layerMask |= 1 << LayerMask.NameToLayer(ln);
    }

    // Detect collision between this sword and other colliders
    void OnTriggerEnter(Collider other)
    {
        // Ignore if not swinging
        if (!active) return;

        // Ignore objects on disallowed layers
        if (((1 << other.gameObject.layer) & layerMask) == 0) return;

        // If object can take damage, apply it
        if (other.TryGetComponent<IDamageable>(out var dmg))
        {
            dmg.TakeDamage(damage);
        }
    }

    // These are optional helper methods you can call
    // from an animation event or your swing script
    public void Enable() => active = true;
    public void Disable() => active = false;
}
