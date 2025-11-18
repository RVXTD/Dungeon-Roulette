using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI")]
    public Slider healthBarSlider;
    public TextMeshProUGUI healthBarValueText;

    [Header("Death Settings")]
    [Tooltip("Scripts to disable when the player dies.")]
    public MonoBehaviour[] scriptsToDisable;
    public GameObject deathUI;

    private bool isDead = false;

    public static bool PlayerIsDead { get; private set; } = false;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    void Start()
    {
        currentHealth = maxHealth;

        if (healthBarSlider != null)
        {
            healthBarSlider.minValue = 0f;
            healthBarSlider.maxValue = maxHealth;
            healthBarSlider.value = currentHealth;
        }

        UpdateHealthUI();
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
        Debug.Log($"Player took {amount} damage. HP: {currentHealth}");

        UpdateHealthUI();

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        Debug.Log($"Player healed {amount}. HP: {currentHealth}");

        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (healthBarSlider != null)
            healthBarSlider.value = currentHealth;

        if (healthBarValueText != null)
            healthBarValueText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}";
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        PlayerIsDead = true;
        Debug.Log("Player has died!");

        foreach (var script in scriptsToDisable)
        {
            if (script != null)
            {
                script.enabled = false;
                Debug.Log($"Disabled: {script.GetType().Name}");
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (deathUI) deathUI.SetActive(true);

        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    public void ResetPlayer()
    {
        isDead = false;
        PlayerIsDead = false;
        currentHealth = maxHealth;
        UpdateHealthUI();
    }
}