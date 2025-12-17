using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityBarUI : MonoBehaviour
{
    [Header("UI")]
    public Slider abilitySlider;          // Ability bar slider
    public TMP_Text abilityNameText;      // Text that shows ability name

    [Header("Refs")]
    public SimplePlayerController playerController;

    void Awake()
    {
        if (playerController == null)
            playerController = FindObjectOfType<SimplePlayerController>();
    }

    void Start()
    {
        if (abilitySlider != null)
        {
            abilitySlider.minValue = 0f;
            abilitySlider.maxValue = 1f;
            abilitySlider.value = 1f;
        }
    }

    void Update()
    {
        if (playerController == null) return;

        // ? ONLY show the ability name
        if (abilityNameText != null)
            abilityNameText.text = playerController.CurrentAbilityName;

        // Bar fill still driven by duration/cooldown
        if (abilitySlider != null)
            abilitySlider.value = playerController.AbilityBar01;
    }
}
