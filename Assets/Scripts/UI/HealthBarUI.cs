using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Player HP bar display.
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;

    private void Awake()
    {
        if (healthSystem == null)
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
                healthSystem = player.GetComponent<HealthSystem>();
        }
    }

    private void OnEnable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnDamage += HandleHealthChanged;
            healthSystem.OnHeal += HandleHealthChanged;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnDamage -= HandleHealthChanged;
            healthSystem.OnHeal -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (healthSystem == null) return;

        float max = Mathf.Max(1f, healthSystem.MaxHp);
        float value = Mathf.Clamp(healthSystem.CurrentHp, 0f, max);

        if (hpSlider != null)
        {
            hpSlider.maxValue = max;
            hpSlider.value = value;
        }

        if (hpFill != null)
            hpFill.fillAmount = value / max;

        if (hpText != null)
            hpText.text = $"{value:0}/{max:0}";
    }
}
