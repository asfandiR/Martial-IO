using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// Level-up UI card display.
public class LevelUpUI : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private LevelUpGenerator levelUpGenerator;
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private WeaponController weaponController;

    [Header("3 choice images")]
    [SerializeField] private Image abilityImage1;
    [SerializeField] private Image abilityImage2;
    [SerializeField] private Image abilityImage3;

    [Header("3 choice buttons")]
    [SerializeField] private Button abilityButton1;
    [SerializeField] private Button abilityButton2;
    [SerializeField] private Button abilityButton3;

    [Header("Stats text")]
    [SerializeField] private TMP_Text currentStatsText;
    [SerializeField] private TMP_Text optionStatsText1;
    [SerializeField] private TMP_Text optionStatsText2;
    [SerializeField] private TMP_Text optionStatsText3;

    private const int ChoiceCount = 3;

    private void Awake()
    {
        TryResolveDependencies();
        Clear();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.LevelUp)
            ShowChoices();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    public void Clear()
    {
        for (int i = 0; i < ChoiceCount; i++)
        {
            var image = GetImageByIndex(i);
            if (image != null)
            {
                image.sprite = null;
                image.enabled = false;
            }

            var button = GetButtonByIndex(i);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
        }

        ClearText(currentStatsText);
        ClearText(optionStatsText1);
        ClearText(optionStatsText2);
        ClearText(optionStatsText3);
    }

    public void SetCurrentStatsText(string currentStats)
    {
        SetText(currentStatsText, currentStats);
    }

    public void SetAbilityOption(int optionIndex, AbilityData ability, UnityAction onClick)
    {
        if (optionIndex < 0 || optionIndex >= ChoiceCount)
            return;

        var image = GetImageByIndex(optionIndex);
        if (image != null)
        {
            image.sprite = ability != null ? ability.icon : null;
            image.enabled = ability != null && ability.icon != null;
        }

        var button = GetButtonByIndex(optionIndex);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(onClick);

            button.interactable = ability != null;
        }

        SetOptionStatsText(optionIndex, BuildAbilityStatsText(ability));
    }

    private string BuildAbilityStatsText(AbilityData ability)
    {
        if (ability == null)
            return "-";

        float damage = abilityManager != null ? abilityManager.GetDamageMultiplierForAbility(ability) : ability.damage;
        float cooldown = abilityManager != null ? abilityManager.GetCooldownMultiplierForAbility(ability) : ability.cooldown;
        float speed = abilityManager != null ? abilityManager.GetProjectileSpeedMultiplierForAbility(ability) : 1f;
        float lifetime = abilityManager != null ? abilityManager.GetProjectileLifetimeMultiplierForAbility(ability) : 1f;
        float critChance = abilityManager != null ? abilityManager.GetCritChanceMultiplierForAbility(ability) : 1f;
        float critDamage = abilityManager != null ? abilityManager.GetCritDamageMultiplierForAbility(ability) : 1f;
        float pierce = abilityManager != null ? abilityManager.GetPierceMultiplierForAbility(ability) : ability.pierceCount;

        float levelGrowth = abilityManager != null ? abilityManager.GetLevelGrowthPercent(ability) : 0f;

        return $"{ability.abilityName}\n" +
               $"Growth/level: +{levelGrowth * 100f:0.#}%\n" +
               $"Damage: {ToDeltaPercent(damage)}\n" +
               $"Cooldown time: {ToDeltaPercent(cooldown)}\n" +
               $"Projectile Speed: {ToDeltaPercent(speed)}\n" +
               $"Pierce: {ToDeltaPercent(pierce)}\n" +
               $"Lifetime: {ToDeltaPercent(lifetime)}\n" +
               $"Crit Chance: {ToDeltaPercent(critChance)}\n" +
               $"Crit Damage: {ToDeltaPercent(critDamage)}";
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.LevelUp)
            ShowChoices();
    }

    private void ShowChoices()
    {
        TryResolveDependencies();
        Clear();

        SetCurrentStatsText(BuildCurrentStatsText());

        if (levelUpGenerator == null)
            return;

        var choices = levelUpGenerator.GenerateChoices();
        if (choices == null)
            return;

        for (int i = 0; i < ChoiceCount; i++)
        {
            AbilityData ability = i < choices.Count ? choices[i] : null;
            var capturedAbility = ability;
            SetAbilityOption(i, capturedAbility, () => SelectAbility(capturedAbility));
        }
    }

    private void SelectAbility(AbilityData ability)
    {
        if (ability != null && abilityManager != null)
            abilityManager.RegisterAbility(ability);

        if (GameManager.Instance != null)
            GameManager.Instance.Resume();
    }

    private string BuildCurrentStatsText()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine("Current stats");

        if (healthSystem != null)
            sb.AppendLine($"HP: {healthSystem.CurrentHp:0}/{healthSystem.MaxHp:0}");

        if (abilityManager != null)
            sb.AppendLine($"Luck: {abilityManager.CurrentLuck * 100f:0.#}%");

        if (playerController != null)
            sb.AppendLine($"Move Speed: {ToPercent(playerController.MoveSpeedMultiplier)}");

        if (weaponController != null)
        {
            sb.AppendLine($"Damage: {ToPercent(weaponController.ProjectileDamageMultiplier)}");
            sb.AppendLine($"Projectile Speed: {ToPercent(weaponController.ProjectileSpeedMultiplier)}");
            sb.AppendLine($"Projectile Lifetime: {ToPercent(weaponController.ProjectileLifetimeMultiplier)}");
            sb.AppendLine($"Pierce: {ToPercent(weaponController.PierceMultiplier)}");
            sb.AppendLine($"Crit Chance: {ToPercent(weaponController.CritChanceMultiplier)}");
            sb.AppendLine($"Crit Damage: {ToPercent(weaponController.CritDamageMultiplier)}");
        }

        if (abilityManager != null)
            sb.AppendLine($"Cooldown time: {ToPercent(abilityManager.CooldownMultiplier)}");

        return sb.ToString().TrimEnd();
    }

    private void TryResolveDependencies()
    {
        if (levelUpGenerator == null)
            levelUpGenerator = Object.FindFirstObjectByType<LevelUpGenerator>();

        if (abilityManager == null)
            abilityManager = Object.FindFirstObjectByType<AbilityManager>();

        if (playerController == null)
            playerController = Object.FindFirstObjectByType<PlayerController>();

        if (weaponController == null)
            weaponController = Object.FindFirstObjectByType<WeaponController>();

        if (healthSystem == null && playerController != null)
            healthSystem = playerController.GetComponent<HealthSystem>();
    }

    private static void ClearText(TMP_Text text)
    {
        if (text != null)
            text.text = string.Empty;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private void SetOptionStatsText(int optionIndex, string value)
    {
        switch (optionIndex)
        {
            case 0:
                SetText(optionStatsText1, value);
                break;
            case 1:
                SetText(optionStatsText2, value);
                break;
            case 2:
                SetText(optionStatsText3, value);
                break;
        }
    }

    private Image GetImageByIndex(int optionIndex)
    {
        switch (optionIndex)
        {
            case 0: return abilityImage1;
            case 1: return abilityImage2;
            case 2: return abilityImage3;
            default: return null;
        }
    }

    private Button GetButtonByIndex(int optionIndex)
    {
        switch (optionIndex)
        {
            case 0: return abilityButton1;
            case 1: return abilityButton2;
            case 2: return abilityButton3;
            default: return null;
        }
    }

    private static string ToPercent(float multiplier)
    {
        return $"{multiplier * 100f:0.#}%";
    }

    private static string ToDeltaPercent(float multiplier)
    {
        float delta = (multiplier - 1f) * 100f;
        return $"{delta:+0.#;-0.#;0}%";
    }
}
