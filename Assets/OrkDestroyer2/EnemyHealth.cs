using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;   // maximum HP
    public float currentHealth;      // current HP

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private EnemyScript enemyScript; // reference to control death behavior

    void Awake()
    {
        // Try to find the EnemyScript on the same GameObject
        enemyScript = GetComponent<EnemyScript>();
    }

    void Start()
    {
        // Start with full health
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        // Subtract damage
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
        Debug.Log($"{name} took {amount} damage. HP: {currentHealth}");

        // When HP hits zero, trigger death logic
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        Debug.Log($"{name} healed {amount}. HP: {currentHealth}");
    }

    private void Die()
    {
        Debug.Log($"{name} has died!");

        // If the EnemyScript exists, use its built-in death sequence
        if (enemyScript != null)
        {
            enemyScript.DoDeath();
        }
        else
        {
            // fallback if enemy script is missing
            Destroy(gameObject);
        }
    }
}