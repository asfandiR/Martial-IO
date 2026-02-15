using System;
using System.Collections.Generic;
using UnityEngine;

// Projectile attack logic: target search, cooldown use, and projectile spawning.
public class ProjectileAttackController : MonoBehaviour
{
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float searchRadius = 8f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private int maxTargets = 32;

    [Header("Projectile Unlock Tokens")]
    [SerializeField] private string[] projectileAbilityTokens =
    {
        "Crossbowman",
        "Archer",
        "Aeromancer",
        "Cryomancer",
        "Druid",
        "Pyromancer",
        "Warlock"
    };

    [Header("Multi Shot")]
    [SerializeField] private int maxMultiShotLevel = 3;

    [Header("Projectile Tuning")]
    [SerializeField, Min(0.1f)] private float projectileSpeedTuning = 1f;

    [Header("Input")]
    [SerializeField] private bool useFireButton = false;

    private Collider2D[] hits;
    private bool isFireButtonHeld;
    private float projectileDamageMultiplier = 1f;
    private float projectileSpeedMultiplier = 1f;
    private float projectileLifetimeMultiplier = 1f;
    private float critChanceMultiplier = 1f;
    private float critDamageMultiplier = 1f;
    private float pierceMultiplier = 1f;

    public float ProjectileDamageMultiplier => projectileDamageMultiplier;
    public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
    public float ProjectileLifetimeMultiplier => projectileLifetimeMultiplier;
    public float CritChanceMultiplier => critChanceMultiplier;
    public float CritDamageMultiplier => critDamageMultiplier;
    public float PierceMultiplier => pierceMultiplier;

    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = GetComponent<AbilityManager>();

        hits = new Collider2D[Mathf.Max(4, maxTargets)];
    }

    private void Update()
    {
        if (abilityManager == null) return;
        if (!IsProjectileUnlocked()) return;
        if (useFireButton && !isFireButtonHeld) return;

        Transform target = FindNearestTarget();
        var abilities = abilityManager.Abilities;

        for (int i = 0; i < abilities.Count; i++)
        {
            if (!abilityManager.IsReady(i)) continue;
            var ability = abilities[i];
            if (ability == null || ability.projectilePrefab == null) continue;
            if (!abilityManager.TryConsumeCooldown(i)) continue;

            FireWithCurrentMultiShot(ability, target);
        }
    }

    public void SetFireButtonHeld(bool value)
    {
        isFireButtonHeld = value;
    }

    public void FireOnceFromButton()
    {
        if (abilityManager == null) return;
        if (!IsProjectileUnlocked()) return;

        Transform target = FindNearestTarget();
        var abilities = abilityManager.Abilities;

        for (int i = 0; i < abilities.Count; i++)
        {
            if (!abilityManager.IsReady(i)) continue;
            var ability = abilities[i];
            if (ability == null || ability.projectilePrefab == null) continue;
            if (!abilityManager.TryConsumeCooldown(i)) continue;

            FireWithCurrentMultiShot(ability, target);
            break;
        }
    }

    public void MultiplyProjectileDamage(float multiplier)
    {
        projectileDamageMultiplier *= Mathf.Max(0.1f, multiplier);
    }

    public void MultiplyProjectileSpeed(float multiplier)
    {
        projectileSpeedMultiplier *= Mathf.Max(0.1f, multiplier);
    }

    public void SetProjectileSpeedTuning(float value)
    {
        projectileSpeedTuning = Mathf.Max(0.1f, value);
    }

    public void MultiplyProjectileLifetime(float multiplier)
    {
        projectileLifetimeMultiplier *= Mathf.Max(0.1f, multiplier);
    }

    public void MultiplyCritChance(float multiplier)
    {
        critChanceMultiplier *= Mathf.Max(0f, multiplier);
    }

    public void MultiplyCritMultiplier(float multiplier)
    {
        critDamageMultiplier *= Mathf.Max(0.1f, multiplier);
    }

    public void MultiplyPierce(float multiplier)
    {
        pierceMultiplier *= Mathf.Max(0.1f, multiplier);
    }

    private void FireWithCurrentMultiShot(AbilityData ability, Transform target)
    {
        var abilities = abilityManager.Abilities;
        int projectileSkillCount = CountProjectileSkills(abilities);
        int multiShotLevel = GetMultiShotLevel(abilities);
        int projectileCount = GetProjectileCount(projectileSkillCount, multiShotLevel);

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 facingDirection = GetFacingDirection();

        if (projectileCount <= 1)
        {
            Fire(ability, origin, facingDirection);
            return;
        }

        Vector3[] directions = BuildMultiShotDirections(projectileCount, facingDirection, target);
        for (int i = 0; i < directions.Length; i++)
            Fire(ability, origin, directions[i]);
    }

    private int GetProjectileCount(int projectileSkillCount, int multiShotLevel)
    {
        if (projectileSkillCount <= 1)
            return 1;

        switch (Mathf.Clamp(multiShotLevel, 0, 3))
        {
            case 1: return 4;
            case 2: return 6;
            case 3: return 8;
            default: return 1;
        }
    }

    private int GetMultiShotLevel(IReadOnlyList<AbilityData> abilities)
    {
        int level = 0;

        for (int i = 0; i < abilities.Count; i++)
        {
            AbilityData ability = abilities[i];
            if (ability == null || ability.projectilePrefab == null) continue;
            if (!IsProjectileAbility(ability)) continue;

            if (ability.rarity == AbilityData.AbilityRarity.Rare
                || ability.rarity == AbilityData.AbilityRarity.Epic
                || ability.rarity == AbilityData.AbilityRarity.Legendary)
            {
                level++;
            }
        }

        return Mathf.Clamp(level, 0, Mathf.Max(0, maxMultiShotLevel));
    }

    private int CountProjectileSkills(IReadOnlyList<AbilityData> abilities)
    {
        int count = 0;

        for (int i = 0; i < abilities.Count; i++)
        {
            AbilityData ability = abilities[i];
            if (ability == null || ability.projectilePrefab == null) continue;
            if (!IsProjectileAbility(ability)) continue;
            count++;
        }

        return count;
    }

    private bool IsProjectileAbility(AbilityData ability)
    {
        if (ability == null || string.IsNullOrWhiteSpace(ability.abilityName))
            return false;

        string name = ability.abilityName;

        for (int i = 0; i < projectileAbilityTokens.Length; i++)
        {
            string token = projectileAbilityTokens[i];
            if (string.IsNullOrWhiteSpace(token)) continue;

            if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private Vector3[] BuildMultiShotDirections(int projectileCount, Vector3 facingDirection, Transform target)
    {
        if (projectileCount == 4)
            return new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down };

        Vector3 baseDirection = facingDirection;
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.z = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                baseDirection = toTarget.normalized;
        }

        float startAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float step = 360f / Mathf.Max(1, projectileCount);
        Vector3[] directions = new Vector3[projectileCount];

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = startAngle + (step * i);
            float radians = angle * Mathf.Deg2Rad;
            directions[i] = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;
        }

        return directions;
    }

    private Transform FindNearestTarget()
    {
        int count = OverlapCircle(transform.position, searchRadius, hits, GetEnemyMask());
        if (count == 0) return null;

        float bestSqr = float.MaxValue;
        Transform best = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            float sqr = (col.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = col.transform;
            }
        }

        return best;
    }

    private void Fire(AbilityData ability, Vector3 origin, Vector3 direction)
    {
        if (ability == null || ability.projectilePrefab == null) return;

        direction.z = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = GetFacingDirection();
        direction.Normalize();

        GameObject proj;
        if (ObjectPooler.Instance != null)
            proj = ObjectPooler.Instance.Get(ability.projectilePrefab, origin, Quaternion.identity);
        else
            proj = Instantiate(ability.projectilePrefab, origin, Quaternion.identity);

        if (proj == null) return;

        var projectile = proj.GetComponent<ProjectileBase>();
        if (projectile != null)
        {
            projectile.SetCombatMultipliers(
                projectileDamageMultiplier,
                projectileSpeedMultiplier * projectileSpeedTuning,
                projectileLifetimeMultiplier,
                critChanceMultiplier,
                critDamageMultiplier,
                pierceMultiplier
            );
            projectile.Init(ability, direction, transform);
        }
    }

    private bool IsProjectileUnlocked()
    {
        if (abilityManager == null) return false;

        for (int i = 0; i < projectileAbilityTokens.Length; i++)
        {
            string token = projectileAbilityTokens[i];
            if (string.IsNullOrWhiteSpace(token)) continue;

            if (abilityManager.HasAbilityNameToken(token))
                return true;
        }

        return false;
    }

    private Vector3 GetFacingDirection()
    {
        float x = transform.lossyScale.x;
        return x < 0f ? Vector3.right : Vector3.left;
    }

    private int GetEnemyMask()
    {
        return enemyMask.value == 0 ? ~0 : enemyMask.value;
    }

    private static int OverlapCircle(Vector2 center, float radius, Collider2D[] buffer, int layerMask)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = layerMask;
        filter.useTriggers = true;
        return Physics2D.OverlapCircle(center, radius, filter, buffer);
    }

    private void OnValidate()
    {
        projectileSpeedTuning = Mathf.Max(0.1f, projectileSpeedTuning);
    }
}
