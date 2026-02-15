using System;

// Container for entity stats.
// Stats: Speed, HP, Damage, Luck
[Serializable]
public struct StatsModifier
{
    public float speed;
    public float hp;
    public float damage;
    public float luck;

    public StatsModifier(float speed, float hp, float damage, float luck)
    {
        this.speed = speed;
        this.hp = hp;
        this.damage = damage;
        this.luck = luck;
    }

    public static StatsModifier operator +(StatsModifier a, StatsModifier b)
    {
        return new StatsModifier(
            a.speed + b.speed,
            a.hp + b.hp,
            a.damage + b.damage,
            a.luck + b.luck
        );
    }

    public static StatsModifier operator *(StatsModifier a, float multiplier)
    {
        return new StatsModifier(
            a.speed * multiplier,
            a.hp * multiplier,
            a.damage * multiplier,
            a.luck * multiplier
        );
    }
}
