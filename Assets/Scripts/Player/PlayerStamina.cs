using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;

    [Header("Drain / Recharge")]
    public float drainPerSecond = 25f;
    public float rechargePerSecond = 30f;
    public float rechargeDelaySeconds = 5f;

    [Header("UI")]
    public Slider staminaSlider;                 // drag your StaminaBar slider here
    public TextMeshProUGUI staminaValueText;     // optional

    private float lastSprintTime = -999f;
    private bool sprintLocked = false;

    public bool CanSprint => !sprintLocked && currentStamina > 0f;

    void Awake()
    {
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }

    void Start()
    {
        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }
        UpdateUI();
    }

    void Update()
    {
        // Recharge only if we haven't sprinted for X seconds
        if (Time.time < lastSprintTime + rechargeDelaySeconds)
            return;

        // Already full?
        if (currentStamina >= maxStamina)
        {
            currentStamina = maxStamina;
            sprintLocked = false; // unlock only when full
            UpdateUI();
            return;
        }

        // Recharge
        currentStamina = Mathf.Min(maxStamina, currentStamina + rechargePerSecond * Time.deltaTime);

        // IMPORTANT: you asked "walk until fully charged"
        if (Mathf.Approximately(currentStamina, maxStamina))
            sprintLocked = false;

        UpdateUI();
    }

    public void DrainWhileSprinting()
    {
        lastSprintTime = Time.time;

        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            sprintLocked = true;
            UpdateUI();
            return;
        }

        currentStamina = Mathf.Max(0f, currentStamina - drainPerSecond * Time.deltaTime);

        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            sprintLocked = true; // force walk until full again
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (staminaSlider != null)
            staminaSlider.value = currentStamina;

        if (staminaValueText != null)
            staminaValueText.text = $"{Mathf.RoundToInt(currentStamina)}/{Mathf.RoundToInt(maxStamina)}";
    }
}
