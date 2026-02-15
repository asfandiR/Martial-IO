using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks active abilities and cooldowns.
// Responsibilities:
// - Register abilities
// - Tick cooldowns
// - Trigger ability use
public class AbilityManager : MonoBehaviour
{
    [SerializeField] private List<AbilityData> abilities = new List<AbilityData>(8);
    private readonly List<float> cooldownTimers = new List<float>(8);

    [Header("Progression")]
    [SerializeField, Range(0f, 1f)] private float baseLuck = 0.35f;
    [SerializeField, Range(0f, 0.05f)] private float luckPerLevel = 0.005f;

    [Header("Per-level player growth (rarity + luck)")]
    [SerializeField, Range(0.05f, 0.15f)] private float commonMinGrowth = 0.05f;
    [SerializeField, Range(0.05f, 0.15f)] private float commonMaxGrowth = 0.08f;
    [SerializeField, Range(0.05f, 0.15f)] private float rareMinGrowth = 0.08f;
    [SerializeField, Range(0.05f, 0.15f)] private float rareMaxGrowth = 0.11f;
    [SerializeField, Range(0.05f, 0.15f)] private float epicMinGrowth = 0.11f;
    [SerializeField, Range(0.05f, 0.15f)] private float epicMaxGrowth = 0.13f;
    [SerializeField, Range(0.05f, 0.15f)] private float legendaryMinGrowth = 0.13f;
    [SerializeField, Range(0.05f, 0.15f)] private float legendaryMaxGrowth = 0.15f;

    [Header("Balance")]
    [SerializeField, Range(0f, 1f)] private float projectileSpeedImpact = 0.15f;
    [SerializeField, Range(0f, 1f)] private float playerSpeedImpact = 0.08f;

    private PlayerController playerController;
    private WeaponController weaponController;
    private ExperienceManager experienceManager;
    private float cooldownMultiplier = 1f;
    [SerializeField] private float swordSectorBonusPerSkill = 15f;

    private const float BaseProjectileSpeed = 10f;
    private const float BaseProjectileLifetime = 3f;
    private const float BaseCritChance = 0.1f;
    private const float BaseCritMultiplier = 2f;

    public IReadOnlyList<AbilityData> Abilities => abilities;
    public float CooldownMultiplier => cooldownMultiplier;
    public float CurrentLuck => GetCurrentLuck();

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        weaponController = GetComponent<WeaponController>();
        experienceManager = FindFirstObjectByType<ExperienceManager>();
        SyncCooldowns();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < cooldownTimers.Count; i++)
        {
            if (cooldownTimers[i] > 0f)
                cooldownTimers[i] = Mathf.Max(0f, cooldownTimers[i] - dt);
        }
    }

    public void RegisterAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (abilities.Contains(ability)) return;

        abilities.Add(ability);
        cooldownTimers.Add(0f);

        ApplyAbilityPercentEffects(ability);
        ApplyAbilitySideEffects(ability);

        if (weaponController != null)
            weaponController.HandleAbilityLearned(ability);
    }

    public void RemoveAbility(AbilityData ability)
    {
        int index = abilities.IndexOf(ability);
        if (index < 0) return;

        abilities.RemoveAt(index);
        cooldownTimers.RemoveAt(index);
    }

    public bool IsReady(int index)
    {
        if (index < 0 || index >= cooldownTimers.Count) return false;
        return cooldownTimers[index] <= 0f;
    }

    public bool TryConsumeCooldown(int index)
    {
        if (index < 0 || index >= abilities.Count) return false;
        if (!IsReady(index)) return false;

        float cd = Mathf.Max(0f, cooldownMultiplier);
        cooldownTimers[index] = cd;
        return true;
    }

    public bool HasAbilityNameToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        for (int i = 0; i < abilities.Count; i++)
        {
            var a = abilities[i];
            if (a == null || string.IsNullOrWhiteSpace(a.abilityName)) continue;
            if (a.abilityName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public void MultiplyCooldown(float multiplier)
    {
        cooldownMultiplier *= Mathf.Clamp(multiplier, 0.85f, 1.2f);
    }

    public float GetLevelGrowthPercent(AbilityData ability)
    {
        if (ability == null) return 0f;

        float luck = GetCurrentLuck();
        GetGrowthRangeByRarity(ability.rarity, out float minGrowth, out float maxGrowth);
        return Mathf.Lerp(minGrowth, maxGrowth, luck);
    }

    public float GetLevelScaleMultiplier(AbilityData ability)
    {
        if (ability == null) return 1f;

        int level = experienceManager != null ? Mathf.Max(1, experienceManager.CurrentLevel) : 1;
        int levelSteps = Mathf.Max(0, level - 1);
        float growth = GetLevelGrowthPercent(ability);

        // Keep level scaling intentionally mild to avoid stat explosion.
        return 1f + (growth * levelSteps * 0.1f);
    }

    public float GetDamageMultiplierForAbility(AbilityData ability)
    {
        if (ability == null) return 1f;

        float baseMul = Mathf.Clamp(ability.damage, 0.8f, 1.25f);
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.35f);
        return baseMul * levelMul;
    }

    public float GetCooldownMultiplierForAbility(AbilityData ability)
    {
        if (ability == null) return 1f;

        float baseMul = Mathf.Clamp(ability.cooldown, 0.85f, 1.2f);
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.35f);
        return Mathf.Clamp(baseMul / levelMul, 0.75f, 1.2f);
    }

    public float GetProjectileSpeedMultiplierForAbility(AbilityData ability)
    {
        if (ability == null) return 1f;

        float normalized = ability.projectileSpeed / BaseProjectileSpeed;
        float soft = 1f + ((normalized - 1f) * projectileSpeedImpact);
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.2f);
        return Mathf.Clamp(soft * levelMul, 0.95f, 1.2f);
    }

    public float GetProjectileLifetimeMultiplierForAbility(AbilityData ability)
    {
        if (ability == null) return 1f;

        float normalized = ability.projectileLifetime / BaseProjectileLifetime;
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.2f);
        return Mathf.Clamp(normalized * levelMul, 0.85f, 1.35f);
    }

    public float GetCritChanceMultiplierForAbility(AbilityData ability)
    {
        if (ability == null) return 1f;

        float normalized = ability.critChance / BaseCritChance;
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.2f);
        return Mathf.Clamp(normalized * levelMul, 0.8f, 1.5f);
    }

    public float GetCritDamageMultiplierForAbility(AbilityData ability)
    {
        if (ability == null) return 1f;

        float normalized = ability.critMultiplier / BaseCritMultiplier;
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.2f);
        return Mathf.Clamp(normalized * levelMul, 0.9f, 1.5f);
    }

    public float GetPierceMultiplierForAbility(AbilityData ability)
    {
        if (ability == null) return 1f;

        float normalized = Mathf.Max(1f, ability.pierceCount);
        float soft = 1f + ((normalized - 1f) * 0.35f);
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.2f);
        return Mathf.Clamp(soft * levelMul, 1f, 2f);
    }

    private void ApplyAbilityPercentEffects(AbilityData ability)
    {
        if (ability == null) return;

        float damageMul = GetDamageMultiplierForAbility(ability);
        float speedMul = GetProjectileSpeedMultiplierForAbility(ability);
        float lifeMul = GetProjectileLifetimeMultiplierForAbility(ability);
        float critChanceMul = GetCritChanceMultiplierForAbility(ability);
        float critDamageMul = GetCritDamageMultiplierForAbility(ability);
        float pierceMul = GetPierceMultiplierForAbility(ability);
        float cooldownMul = GetCooldownMultiplierForAbility(ability);

        if (weaponController != null)
        {
            weaponController.MultiplyProjectileDamage(damageMul);
            weaponController.MultiplyProjectileSpeed(speedMul);
            weaponController.MultiplyProjectileLifetime(lifeMul);
            weaponController.MultiplyCritChance(critChanceMul);
            weaponController.MultiplyCritMultiplier(critDamageMul);
            weaponController.MultiplyPierce(pierceMul);
        }

        if (playerController != null)
        {
            float moveSpeedMul = 1f + ((speedMul - 1f) * playerSpeedImpact);
            playerController.MultiplyMoveSpeed(Mathf.Clamp(moveSpeedMul, 0.97f, 1.03f));
        }

        MultiplyCooldown(cooldownMul);
    }

    private void ApplyAbilitySideEffects(AbilityData ability)
    {
        if (ability == null || string.IsNullOrWhiteSpace(ability.abilityName))
            return;

        string name = ability.abilityName;
        bool isDemon = name.IndexOf("Demon skill", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isDebuff = name.IndexOf("DeBuff skill", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Debuff skill", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isSwordMastery = name.IndexOf("Berserker skill", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Paladin skill", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Swordsman skill", StringComparison.OrdinalIgnoreCase) >= 0;

        if (isDemon)
        {
            if (weaponController != null) weaponController.MultiplyProjectileDamage(1.1f);
            if (playerController != null) playerController.MultiplyMoveSpeed(0.98f);
            return;
        }

        if (isDebuff)
        {
            if (weaponController != null) weaponController.MultiplyProjectileDamage(0.95f);
            if (playerController != null) playerController.MultiplyMoveSpeed(0.98f);
            MultiplyCooldown(1.05f);
            return;
        }

        if (isSwordMastery && weaponController != null)
            weaponController.AddSwordSectorAngle(swordSectorBonusPerSkill);
    }

    private float GetCurrentLuck()
    {
        int level = experienceManager != null ? Mathf.Max(1, experienceManager.CurrentLevel) : 1;
        return Mathf.Clamp01(baseLuck + (level - 1) * luckPerLevel);
    }

    private void GetGrowthRangeByRarity(AbilityData.AbilityRarity rarity, out float minGrowth, out float maxGrowth)
    {
        switch (rarity)
        {
            case AbilityData.AbilityRarity.Rare:
                minGrowth = Mathf.Min(rareMinGrowth, rareMaxGrowth);
                maxGrowth = Mathf.Max(rareMinGrowth, rareMaxGrowth);
                break;
            case AbilityData.AbilityRarity.Epic:
                minGrowth = Mathf.Min(epicMinGrowth, epicMaxGrowth);
                maxGrowth = Mathf.Max(epicMinGrowth, epicMaxGrowth);
                break;
            case AbilityData.AbilityRarity.Legendary:
                minGrowth = Mathf.Min(legendaryMinGrowth, legendaryMaxGrowth);
                maxGrowth = Mathf.Max(legendaryMinGrowth, legendaryMaxGrowth);
                break;
            default:
                minGrowth = Mathf.Min(commonMinGrowth, commonMaxGrowth);
                maxGrowth = Mathf.Max(commonMinGrowth, commonMaxGrowth);
                break;
        }
    }

    private void SyncCooldowns()
    {
        cooldownTimers.Clear();
        for (int i = 0; i < abilities.Count; i++)
            cooldownTimers.Add(0f);
    }
}
