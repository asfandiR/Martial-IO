﻿using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks active abilities and cooldowns.
// Responsibilities:
// - Register abilities
// - Tick cooldowns
// - Trigger ability use
public class AbilityManager : MonoBehaviour
{
    public enum AbilityStat
    {
        Damage,
        Cooldown,
        ProjectileSpeed,
        ProjectileLifetime,
        CritChance,
        CritDamage,
        Pierce
    }

    public static readonly AbilityStat[] AllStats =
    {
        AbilityStat.Damage,
        AbilityStat.Cooldown,
        AbilityStat.ProjectileSpeed,
        AbilityStat.ProjectileLifetime,
        AbilityStat.CritChance,
        AbilityStat.CritDamage,
        AbilityStat.Pierce
    };

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
    [SerializeField, Range(0f, 1f)] private float playerSpeedImpact = 0.08f;

    private PlayerController playerController;
    private WeaponController weaponController;
    private ExperienceManager experienceManager;
    private float cooldownMultiplier = 1f;
    [SerializeField, Min(0f)] private float projectileCooldownBase = 2f;
    [SerializeField] private float swordSectorBonusPerSkill = 15f;

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

        float cd = GetProjectileCooldownDuration();
        cooldownTimers[index] = cd;
        return true;
    }

    public float GetProjectileCooldownDuration()
    {
        return Mathf.Max(0f, projectileCooldownBase * cooldownMultiplier);
    }

    public bool HasAbilityTag(AbilityTag tag)
    {
        if (tag == AbilityTag.None) return false;

        for (int i = 0; i < abilities.Count; i++)
        {
            var a = abilities[i];
            if (a == null || a.tags == null) continue;
            if (a.tags.Contains(tag))
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
        return GetAbilityStatMultiplier(ability, AbilityStat.Damage);
    }

    public float GetCooldownMultiplierForAbility(AbilityData ability)
    {
        return GetAbilityStatMultiplier(ability, AbilityStat.Cooldown);
    }

    public float GetProjectileSpeedMultiplierForAbility(AbilityData ability)
    {
        return GetAbilityStatMultiplier(ability, AbilityStat.ProjectileSpeed);
    }

    public float GetProjectileLifetimeMultiplierForAbility(AbilityData ability)
    {
        return GetAbilityStatMultiplier(ability, AbilityStat.ProjectileLifetime);
    }

    public float GetCritChanceMultiplierForAbility(AbilityData ability)
    {
        return GetAbilityStatMultiplier(ability, AbilityStat.CritChance);
    }

    public float GetCritDamageMultiplierForAbility(AbilityData ability)
    {
        return GetAbilityStatMultiplier(ability, AbilityStat.CritDamage);
    }

    public float GetPierceMultiplierForAbility(AbilityData ability)
    {
        return GetAbilityStatMultiplier(ability, AbilityStat.Pierce);
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
            weaponController.MultiplySwordDamage(damageMul);
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
        if (ability == null || ability.tags == null)
            return;

        for (int i = 0; i < ability.tags.Count; i++)
        {
            AbilityTag tag = ability.tags[i];
            switch (tag)
            {
                // --- Projectile Families ---
                case AbilityTag.Crossbowman: // Heavy bolts -> Pierce
                    if (weaponController != null) weaponController.MultiplyPierce(1.25f);
                    break;
                case AbilityTag.Archer: // Precision -> Crit Chance
                    if (weaponController != null) weaponController.MultiplyCritChance(1.15f);
                    break;
                case AbilityTag.Aeromancer: // Wind -> Speed (Projectile + Move)
                    if (weaponController != null) weaponController.MultiplyProjectileSpeed(1.12f);
                    if (playerController != null) playerController.MultiplyMoveSpeed(1.02f);
                    break;
                case AbilityTag.Cryomancer: // Ice -> Lingering presence (Lifetime)
                    if (weaponController != null) weaponController.MultiplyProjectileLifetime(1.20f);
                    break;
                case AbilityTag.Pyromancer: // Fire -> Raw Damage
                    if (weaponController != null) weaponController.MultiplyProjectileDamage(1.10f);
                    break;
                case AbilityTag.Warlock: // Dark Arts -> Crit Damage
                    if (weaponController != null) weaponController.MultiplyCritMultiplier(1.15f);
                    break;
                case AbilityTag.Druid: // Nature -> Growth (Cooldown)
                    MultiplyCooldown(0.96f);
                    break;

                // --- Sword Families ---
                case AbilityTag.Swordsman: // Technique -> Orbit Speed
                    if (weaponController != null) weaponController.MultiplySwordOrbitSpeed(1.10f);
                    break;
                case AbilityTag.Berserker: // Rage -> Raw Damage
                    if (weaponController != null) weaponController.MultiplySwordDamage(1.20f);
                    break;
                case AbilityTag.Paladin: // Protection -> Sector Coverage
                    if (weaponController != null) weaponController.AddSwordSectorAngle(swordSectorBonusPerSkill);
                    break;
                case AbilityTag.Spearman: // Reach -> Orbit Radius
                    if (weaponController != null) weaponController.AddSwordRadius(0.35f);
                    break;
                case AbilityTag.Blacksmith: // Quality -> Speed + Damage mix
                    if (weaponController != null)
                    {
                        weaponController.MultiplySwordDamage(1.05f);
                        weaponController.MultiplySwordOrbitSpeed(1.05f);
                    }
                    break;

                // --- Specials ---
                case AbilityTag.Demon: // Power at a cost
                    if (weaponController != null)
                    {
                        weaponController.MultiplyProjectileDamage(1.15f);
                        weaponController.MultiplySwordDamage(1.15f);
                    }
                    if (playerController != null) playerController.MultiplyMoveSpeed(0.98f);
                    break;
                case AbilityTag.Debuff: // Cursed power
                    MultiplyCooldown(1.05f); // Penalty
                    if (weaponController != null) weaponController.MultiplyProjectileDamage(1.25f); // But high damage
                    break;
                case AbilityTag.Buff: // Utility
                    if (playerController != null) playerController.MultiplyMoveSpeed(1.04f);
                    break;
                case AbilityTag.Priest: // Holy -> Cooldowns (in addition to AutoExplosion)
                    MultiplyCooldown(0.98f);
                    break;
            }
        }
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

    private float GetAbilityStatMultiplier(AbilityData ability, AbilityStat stat)
    {
        if (ability == null)
            return 1f;

        if (!HasBoostedStat(ability, stat))
            return 1f;

        float growth = Mathf.Max(0f, GetLevelGrowthPercent(ability));
        float levelMul = Mathf.Clamp(GetLevelScaleMultiplier(ability), 1f, 1.35f);
        float delta = Mathf.Clamp(growth * levelMul * 2f, 0.02f, 0.7f);
        bool debuff = IsDebuffAbility(ability);

        if (stat == AbilityStat.Cooldown)
        {
            float cooldownMul = debuff ? 1f + delta : 1f - delta;
            return Mathf.Clamp(cooldownMul, 0.3f, 1.7f);
        }

        float mul = debuff ? 1f - delta : 1f + delta;
        return Mathf.Clamp(mul, 0.3f, 1.7f);
    }

    public bool HasBoostedStat(AbilityData ability, AbilityStat stat)
    {
        if (ability == null)
            return false;

        int statCount = AllStats.Length;
        if (statCount == 0)
            return false;

        int targetCount = Mathf.Clamp(GetBoostedStatCountByRarity(ability.rarity), 1, statCount);
        int seed = Mathf.Abs(GetStableHash(GetAbilityStableId(ability)));
        int cursor = seed % statCount;
        int step = (seed % (statCount - 1)) + 1;
        bool[] selected = new bool[statCount];

        for (int picked = 0; picked < targetCount; picked++)
        {
            int safety = 0;
            while (selected[cursor] && safety < statCount)
            {
                cursor = (cursor + 1) % statCount;
                safety++;
            }

            selected[cursor] = true;
            if (AllStats[cursor] == stat)
                return true;

            cursor = (cursor + step) % statCount;
        }

        return false;
    }

    private static int GetBoostedStatCountByRarity(AbilityData.AbilityRarity rarity)
    {
        switch (rarity)
        {
            case AbilityData.AbilityRarity.Rare:
                return 2;
            case AbilityData.AbilityRarity.Epic:
                return 3;
            case AbilityData.AbilityRarity.Legendary:
                return 4;
            default:
                return 1;
        }
    }

    private static string GetAbilityStableId(AbilityData ability)
    {
        if (ability == null)
            return string.Empty;

        string name = string.IsNullOrWhiteSpace(ability.abilityName) ? ability.name : ability.abilityName;
        string iconName = ability.icon != null ? ability.icon.name : "none";
        return $"{name}|{ability.rarity}|{iconName}";
    }

    private static int GetStableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = (hash * 31) + value[i];
            return hash;
        }
    }

    private static bool IsDebuffAbility(AbilityData ability)
    {
        if (ability == null || ability.tags == null)
            return false;

        return ability.tags.Contains(AbilityTag.Debuff);
    }
}
