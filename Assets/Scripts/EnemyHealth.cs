using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    void Start()
    {
        // Initialize health to full when the enemy spawns
        currentHealth = maxHealth;
    }

    // Call this when the enemy takes damage
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"{gameObject.name} took {amount} damage. Current health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Call this when the enemy is healed
    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"{gameObject.name} healed by {amount}. Current health: {currentHealth}");
    }

    // What happens when the enemy dies
    private void Die()
    {
        Debug.Log($"{gameObject.name} has died!");
        // Add death behavior here (e.g., play animation, destroy object, etc.)
        // Destroy(gameObject);
    }
}
