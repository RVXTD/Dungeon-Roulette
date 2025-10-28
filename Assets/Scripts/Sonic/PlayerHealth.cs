using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;   // Max player health
    public float currentHealth;      // Current health

    [Header("Damage Cooldown")]
    public float invulnSeconds = 0.5f;  // Short invulnerability period after hit
    float invulnTimer;                  // Internal timer

    // Implementing IDamageable
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    void Awake()
    {
        // Start fully healed
        currentHealth = maxHealth;
    }

    void Update()
    {
        // Count down invulnerability timer
        if (invulnTimer > 0)
            invulnTimer -= Time.deltaTime;
    }

    // Apply incoming damage
    public void TakeDamage(float amount)
    {
        // Ignore damage during invulnerability
        if (invulnTimer > 0)
            return;

        // Subtract health safely
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);

        // Reset invulnerability window
        invulnTimer = invulnSeconds;

        Debug.Log($"PLAYER took {amount} damage. HP: {currentHealth}");

        // Trigger death logic
        if (currentHealth <= 0)
            Die();
    }

    // Heal the player
    public void Heal(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        Debug.Log($"PLAYER healed {amount}. HP: {currentHealth}");
    }

    // Handle player death
    void Die()
    {
        Debug.Log("PLAYER has died!");
        // TODO: reload scene, show game over, etc.
    }
}