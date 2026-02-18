using System.Collections.Generic;
using UnityEngine;

// Applies and removes temporary stat buffs from collected effectors.
[RequireComponent(typeof(PlayerController))]
public class EffectorRuntimeController : MonoBehaviour
{
    public readonly struct ActiveEffectorSnapshot
    {
        public readonly EffectorSO source;
        public readonly EffectorSO.BoostedStat stat;
        public readonly float remainingSeconds;

        public ActiveEffectorSnapshot(EffectorSO source, EffectorSO.BoostedStat stat, float remainingSeconds)
        {
            this.source = source;
            this.stat = stat;
            this.remainingSeconds = remainingSeconds;
        }
    }

    private sealed class ActiveBuff
    {
        public EffectorSO source;
        public EffectorSO.BoostedStat stat;
        public float multiplier;
        public float endTime;
        public float healTickAt;
        public float flatDamageReduction;
    }

    private readonly List<ActiveBuff> activeBuffs = new List<ActiveBuff>(16);
    private WeaponController weapon;
    private AbilityManager abilities;
    private HealthSystem health;

    private void Awake()
    {
        weapon = GetComponent<WeaponController>();
        abilities = GetComponent<AbilityManager>();
        health = GetComponent<HealthSystem>();
    }

    private void Update()
    {
        if (activeBuffs.Count == 0)
            return;

        float now = Time.time;
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = activeBuffs[i];
            if (buff == null)
                continue;

            if (buff.stat == EffectorSO.BoostedStat.Healing && health != null)
            {
                float healUntil = Mathf.Min(now, buff.endTime);
                while (buff.healTickAt <= healUntil)
                {
                    health.Heal(1f);
                    buff.healTickAt += 1f;
                }
            }

            if (now < buff.endTime)
                continue;

            activeBuffs.RemoveAt(i);
            RemoveBuff(buff);
        }
    }

    public void ApplyEffector(EffectorSO effector)
    {
        if (effector == null)
            return;

        float delta = Mathf.Max(0f, effector.boostPercent) * 0.01f;
        float multiplier = 1f + delta;
        if (effector.boostedStat == EffectorSO.BoostedStat.HalfCooldown)
            multiplier = Mathf.Max(0.1f, 1f - delta);

        var buff = new ActiveBuff
        {
            source = effector,
            stat = effector.boostedStat,
            multiplier = multiplier,
            endTime = Time.time + Mathf.Max(0.1f, effector.DurationSeconds),
            healTickAt = Time.time + 1f,
            flatDamageReduction = 0f
        };

        if (effector.boostedStat == EffectorSO.BoostedStat.Shielding)
            buff.flatDamageReduction = GetShieldDamageReduction(effector.rarity);

        activeBuffs.Add(buff);
        ApplyBuff(buff);
    }

    public int GetActiveEffectors(List<ActiveEffectorSnapshot> target)
    {
        if (target == null)
            return 0;

        target.Clear();
        if (activeBuffs.Count == 0)
            return 0;

        float now = Time.time;
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            ActiveBuff buff = activeBuffs[i];
            if (buff == null)
                continue;

            float remaining = buff.endTime - now;
            if (remaining <= 0f)
                continue;

            target.Add(new ActiveEffectorSnapshot(buff.source, buff.stat, remaining));
        }

        return target.Count;
    }

    private void ApplyBuff(ActiveBuff buff)
    {
        if (buff == null)
            return;

        switch (buff.stat)
        {
            case EffectorSO.BoostedStat.X2Damage:
                if (weapon != null)
                {
                    weapon.MultiplyProjectileDamage(buff.multiplier);
                    weapon.MultiplySwordDamage(buff.multiplier);
                }
                break;
            case EffectorSO.BoostedStat.HalfCooldown:
                if (abilities != null)
                    abilities.MultiplyCooldown(buff.multiplier);
                break;
            case EffectorSO.BoostedStat.X2CritChance:
                if (weapon != null)
                    weapon.MultiplyCritChance(buff.multiplier);
                break;
            case EffectorSO.BoostedStat.Healing:
                break;
            case EffectorSO.BoostedStat.Shielding:
                RecalculateShielding();
                break;
        }
    }

    private void RemoveBuff(ActiveBuff buff)
    {
        if (buff == null)
            return;

        if (buff.stat == EffectorSO.BoostedStat.X2Damage)
        {
            float inverse = buff.multiplier <= 0.0001f ? 1f : 1f / buff.multiplier;
            if (weapon != null)
            {
                weapon.MultiplyProjectileDamage(inverse);
                weapon.MultiplySwordDamage(inverse);
            }
            return;
        }

        if (buff.stat == EffectorSO.BoostedStat.HalfCooldown)
        {
            float inverse = buff.multiplier <= 0.0001f ? 1f : 1f / buff.multiplier;
            if (abilities != null)
                abilities.MultiplyCooldown(inverse);
            return;
        }

        if (buff.stat == EffectorSO.BoostedStat.X2CritChance)
        {
            float inverse = buff.multiplier <= 0.0001f ? 1f : 1f / buff.multiplier;
            if (weapon != null)
                weapon.MultiplyCritChance(inverse);
            return;
        }

        if (buff.stat == EffectorSO.BoostedStat.Shielding)
            RecalculateShielding();
    }

    private static float GetShieldDamageReduction(EffectorSO.EffectorRarity rarity)
    {
        switch (rarity)
        {
            case EffectorSO.EffectorRarity.Rare:
                return 4f;
            case EffectorSO.EffectorRarity.Epic:
                return 8f;
            case EffectorSO.EffectorRarity.Legendary:
                return 10f;
            default:
                return 2f;
        }
    }

    private void RecalculateShielding()
    {
        if (health == null)
            return;

        float totalReduction = 0f;
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            var buff = activeBuffs[i];
            if (buff == null || buff.stat != EffectorSO.BoostedStat.Shielding)
                continue;

            totalReduction += Mathf.Max(0f, buff.flatDamageReduction);
        }

        health.SetIncomingFlatDamageReduction(totalReduction);
    }
}
