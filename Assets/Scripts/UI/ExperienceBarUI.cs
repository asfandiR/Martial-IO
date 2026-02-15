using UnityEngine;
using UnityEngine.UI;
using TMPro;

// XP bar display.
public class ExperienceBarUI : MonoBehaviour
{
    [SerializeField] private ExperienceManager experienceManager;
    [SerializeField] private Slider xpSlider;
    [SerializeField] private Image xpFill;
    [SerializeField] private TMP_Text xpText;

    private void Awake()
    {
        if (experienceManager == null)
            experienceManager = Object.FindFirstObjectByType<ExperienceManager>();
    }

    private void OnEnable()
    {
        if (experienceManager != null)
            experienceManager.OnXpChanged += HandleXpChanged;
    }

    private void Start()
    {
        if (experienceManager != null)
            HandleXpChanged(experienceManager.CurrentXp, experienceManager.XpToNext);
    }

    private void OnDisable()
    {
        if (experienceManager != null)
            experienceManager.OnXpChanged -= HandleXpChanged;
    }

    private void HandleXpChanged(int currentXp, int xpToNext)
    {
        float max = Mathf.Max(1, xpToNext);
        float value = Mathf.Clamp(currentXp, 0, xpToNext);

        if (xpSlider != null)
        {
            xpSlider.maxValue = max;
            xpSlider.value = value;
        }

        if (xpFill != null)
            xpFill.fillAmount = value / max;

        if (xpText != null)
            xpText.text = $"{value:0}/{max:0}";
    }
}
