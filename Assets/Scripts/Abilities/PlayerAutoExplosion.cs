﻿using UnityEngine;

// Automatic player explosion ability driven by owned Priest skills.
[RequireComponent(typeof(AbilityManager))]
public class PlayerAutoExplosion : MonoBehaviour
{
    [Header("Explosion Setup")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private bool castOnStart = true;
    [SerializeField] private bool requirePriestSkill = true;

    [Header("Base Stats")]
    [SerializeField, Min(0.05f)] private float baseCooldown = 8f;
    [SerializeField, Min(0f)] private float minCooldown = 1.25f;
    [SerializeField, Min(0f)] private float baseDamage = 10f;
    [SerializeField, Min(0.1f)] private float baseRadius = 1.6f;

    [Header("Scaling Per Priest Point")]
    [SerializeField, Min(0f)] private float cooldownReductionPerPoint = 0.2f;
    [SerializeField, Min(0f)] private float damagePerPoint = 2f;
    [SerializeField, Min(0f)] private float radiusPerPoint = 0.12f;

    [Header("Priest Rarity Weights")]
    [SerializeField, Min(0)] private int commonPoints = 1;
    [SerializeField, Min(0)] private int rarePoints = 2;
    [SerializeField, Min(0)] private int epicPoints = 3;
    [SerializeField, Min(0)] private int legendaryPoints = 4;

    [Header("Explosion Timing")]
    [SerializeField, Min(0f)] private float startRadius = 0.1f;
    [SerializeField, Min(0.05f)] private float expandDuration = 0.2f;
    [SerializeField, Min(0.05f)] private float totalLifetime = 0.35f;

    [Header("Priest Detection")]
    [SerializeField] private AbilityTag priestAbilityTag = AbilityTag.Priest;

    private AbilityManager abilityManager;
    private HealthSystem health;
    private float cooldownTimer;
    private Coroutine explosionRoutine;

    private void Awake()
    {
        abilityManager = GetComponent<AbilityManager>();
        health = GetComponent<HealthSystem>();
    }

    private void OnEnable()
    {
        cooldownTimer = castOnStart ? 0f : baseCooldown;
        if (explosionRoutine != null)
            StopCoroutine(explosionRoutine);
        explosionRoutine = StartCoroutine(ExplosionLoop());
    }

    private void OnDisable()
    {
        if (explosionRoutine != null)
        {
            StopCoroutine(explosionRoutine);
            explosionRoutine = null;
        }
    }

    private System.Collections.IEnumerator ExplosionLoop()
    {
        while (enabled && gameObject.activeInHierarchy)
        {
            if (explosionPrefab == null || abilityManager == null)
            {
                yield return null;
                continue;
            }

            if (health != null && health.IsDead)
            {
                yield return null;
                continue;
            }

            ExplosionStats stats = BuildStats();
            if (requirePriestSkill && stats.priestSkillCount <= 0)
            {
                yield return null;
                continue;
            }

            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                SpawnExplosion(stats);
                cooldownTimer = stats.cooldown;
            }

            yield return null;
        }

        explosionRoutine = null;
    }

    private ExplosionStats BuildStats()
    {
        int priestCount = 0;
        int points = 0;

        var abilities = abilityManager.Abilities;
        for (int i = 0; i < abilities.Count; i++)
        {
            AbilityData ability = abilities[i];
            if (!IsPriestAbility(ability))
                continue;

            priestCount++;
            points += GetRarityPoints(ability.rarity);
        }

        ExplosionStats stats = new ExplosionStats();
        stats.priestSkillCount = priestCount;
        stats.damage = baseDamage + points * damagePerPoint;
        stats.radius = baseRadius + points * radiusPerPoint;
        stats.cooldown = Mathf.Max(minCooldown, baseCooldown - points * cooldownReductionPerPoint);
        return stats;
    }

    private void SpawnExplosion(ExplosionStats stats)
    {
        GameObject go = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        if (go == null)
            return;

        if (!go.TryGetComponent(out ExpandingExplosion2D expandingExplosion))
            expandingExplosion = go.AddComponent<ExpandingExplosion2D>();

        expandingExplosion.Initialize(
            ownerRoot: transform.root,
            damage: stats.damage,
            startRadius: startRadius,
            targetRadius: stats.radius,
            expandDuration: expandDuration,
            totalLifetime: totalLifetime,
            enemyLayerMask: enemyMask.value == 0 ? ~0 : enemyMask.value
        );
    }

    private bool IsPriestAbility(AbilityData ability)
    {
        if (ability == null || ability.tags == null)
            return false;

        return ability.tags.Contains(priestAbilityTag);
    }

    private int GetRarityPoints(AbilityData.AbilityRarity rarity)
    {
        switch (rarity)
        {
            case AbilityData.AbilityRarity.Rare:
                return rarePoints;
            case AbilityData.AbilityRarity.Epic:
                return epicPoints;
            case AbilityData.AbilityRarity.Legendary:
                return legendaryPoints;
            default:
                return commonPoints;
        }
    }

    private struct ExplosionStats
    {
        public int priestSkillCount;
        public float damage;
        public float radius;
        public float cooldown;
    }
}
