using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Death Settings")]
    [Tooltip("Scripts to disable when the player dies.")]
    public MonoBehaviour[] scriptsToDisable; // drag your movement/camera scripts here
    public GameObject deathUI;               // optional

    private bool isDead = false;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
        Debug.Log($"Player took {amount} damage. HP: {currentHealth}");

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Player has died!");

        // Disable movement/camera scripts
        foreach (var script in scriptsToDisable)
        {
            if (script != null)
            {
                script.enabled = false;
                Debug.Log($"Disabled: {script.GetType().Name}");
            }
        }

        // Unlock mouse for menus or respawn
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Show death UI if assigned
        if (deathUI) deathUI.SetActive(true);

        // Stop physics movement if Rigidbody present
        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        Debug.Log($"Player healed {amount}. HP: {currentHealth}");
    }
}